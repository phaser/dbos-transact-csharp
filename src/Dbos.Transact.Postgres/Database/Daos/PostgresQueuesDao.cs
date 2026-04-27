using System.Data;
using System.Data.Common;
using Dapper;
using Dbos.Transact.Database.Daos;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Postgres.Database.Daos;

/// <summary>
/// PostgreSQL-backed implementation of <see cref="QueuesDao"/>.
/// Uses <c>FOR UPDATE SKIP LOCKED</c> (or <c>NOWAIT</c> with global concurrency)
/// and <c>REPEATABLE READ</c> isolation for safe concurrent dequeue.
/// Port of Java's <c>QueuesDAO</c>.
/// </summary>
public sealed class PostgresQueuesDao : QueuesDao
{
    private readonly Func<DbConnection> _connectionFactory;
    private readonly string _schemaPrefix;

    public PostgresQueuesDao(Func<DbConnection> connectionFactory, string schema)
    {
        _connectionFactory = connectionFactory;
        _schemaPrefix = string.IsNullOrEmpty(schema) ? string.Empty : $"\"{schema}\".";
    }

    private sealed class PendingCountRow
    {
        public string? ExecutorId { get; set; }
        public int Count { get; set; }
    }

    public override async Task<bool> ClearQueueAssignmentAsync(string workflowId, CancellationToken ct = default)
    {
        var sql = $"""
            UPDATE {_schemaPrefix}workflow_status
               SET started_at_epoch_ms = NULL,
                   status = @Enqueued
             WHERE workflow_uuid = @WorkflowId
               AND queue_name IS NOT NULL
               AND status = @Pending
            """;
        await using var connection = _connectionFactory();
        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Enqueued = WorkflowState.Enqueued.ToDbString(),
            Pending = WorkflowState.Pending.ToDbString(),
            WorkflowId = workflowId,
        }, cancellationToken: ct)).ConfigureAwait(false);
        return affected > 0;
    }

    public override async Task<IReadOnlyList<string>> GetQueuePartitionsAsync(
        string queueName, CancellationToken ct = default)
    {
        var sql = $"""
            SELECT DISTINCT queue_partition_key
            FROM {_schemaPrefix}workflow_status
            WHERE queue_name = @QueueName
              AND status = @Enqueued
              AND queue_partition_key IS NOT NULL
            """;
        await using var connection = _connectionFactory();
        var partitions = await connection.QueryAsync<string>(new CommandDefinition(sql, new
        {
            QueueName = queueName,
            Enqueued = WorkflowState.Enqueued.ToDbString(),
        }, cancellationToken: ct)).ConfigureAwait(false);
        return partitions.ToList();
    }

    public override async Task<IReadOnlyList<string>> GetAndStartQueuedWorkflowsAsync(
        Queue queue, string executorId, string? appVersion, string? partitionKey,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(partitionKey))
            partitionKey = null;

        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, ct).ConfigureAwait(false);

        // 1. Rate-limit check
        int numRecentQueries = 0;
        if (queue.RateLimit is not null)
        {
            var cutoffMs = DateTimeOffset.UtcNow.Subtract(queue.RateLimit.Period).ToUnixTimeMilliseconds();
            var limiterSql = new System.Text.StringBuilder($"""
                SELECT COUNT(*)
                FROM {_schemaPrefix}workflow_status
                WHERE queue_name = @QueueName
                  AND status NOT IN (@Enqueued, @Delayed)
                  AND started_at_epoch_ms > @CutoffMs
                """);
            if (partitionKey is not null)
                limiterSql.Append(" AND queue_partition_key = @PartitionKey");

            numRecentQueries = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(limiterSql.ToString(), new
                {
                    QueueName = queue.Name,
                    Enqueued = WorkflowState.Enqueued.ToDbString(),
                    Delayed = WorkflowState.Delayed.ToDbString(),
                    CutoffMs = cutoffMs,
                    PartitionKey = partitionKey,
                }, transaction: tx, cancellationToken: ct)).ConfigureAwait(false);

            if (numRecentQueries >= queue.RateLimit.Limit)
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                return Array.Empty<string>();
            }
        }

        // 2. Concurrency-limit check
        int maxTasks = 100;
        if (queue.WorkerConcurrency is not null || queue.Concurrency is not null)
        {
            var pendingSql = new System.Text.StringBuilder($"""
                SELECT executor_id AS ExecutorId, COUNT(*) AS Count
                FROM {_schemaPrefix}workflow_status
                WHERE queue_name = @QueueName AND status = @Pending
                """);
            if (partitionKey is not null)
                pendingSql.Append(" AND queue_partition_key = @PartitionKey");
            pendingSql.Append(" GROUP BY executor_id");

            var pendingRows = await connection.QueryAsync<PendingCountRow>(
                new CommandDefinition(pendingSql.ToString(), new
                {
                    QueueName = queue.Name,
                    Pending = WorkflowState.Pending.ToDbString(),
                    PartitionKey = partitionKey,
                }, transaction: tx, cancellationToken: ct)).ConfigureAwait(false);

            var pendingByExecutor = pendingRows
                .Where(r => r.ExecutorId is not null)
                .ToDictionary(r => r.ExecutorId!, r => r.Count);
            int localPending = pendingByExecutor.GetValueOrDefault(executorId, 0);

            if (queue.WorkerConcurrency is not null)
                maxTasks = Math.Max(0, queue.WorkerConcurrency.Value - localPending);

            if (queue.Concurrency is not null)
            {
                int globalPending = pendingByExecutor.Values.Sum();
                int available = Math.Max(0, queue.Concurrency.Value - globalPending);
                if (available < maxTasks)
                    maxTasks = available;
            }
        }

        if (maxTasks <= 0)
        {
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            return Array.Empty<string>();
        }

        // 3. SELECT workflows to dequeue
        var lockClause = queue.Concurrency is null ? "FOR UPDATE SKIP LOCKED" : "FOR UPDATE NOWAIT";
        var orderClause = queue.PriorityEnabled
            ? "ORDER BY priority ASC, created_at ASC"
            : "ORDER BY created_at ASC";

        var selectSql = new System.Text.StringBuilder($"""
            SELECT workflow_uuid
            FROM {_schemaPrefix}workflow_status
            WHERE queue_name = @QueueName
              AND status = @Enqueued
              AND (application_version = @AppVersion OR application_version IS NULL)
            """);
        if (partitionKey is not null)
            selectSql.Append(" AND queue_partition_key = @PartitionKey");
        selectSql.Append(' ').Append(orderClause).Append(' ').Append(lockClause).Append(" LIMIT @MaxTasks");

        IEnumerable<string> dequeuedIds;
        try
        {
            dequeuedIds = await connection.QueryAsync<string>(
                new CommandDefinition(selectSql.ToString(), new
                {
                    QueueName = queue.Name,
                    Enqueued = WorkflowState.Enqueued.ToDbString(),
                    AppVersion = appVersion,
                    PartitionKey = partitionKey,
                    MaxTasks = maxTasks,
                }, transaction: tx, cancellationToken: ct)).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            return Array.Empty<string>();
        }

        // 4. UPDATE selected workflows to PENDING
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var updateSql = $"""
            UPDATE {_schemaPrefix}workflow_status
               SET status = @Pending,
                   application_version = @AppVersion,
                   executor_id = @ExecutorId,
                   started_at_epoch_ms = @Now,
                   workflow_deadline_epoch_ms = CASE
                       WHEN workflow_timeout_ms IS NOT NULL AND workflow_deadline_epoch_ms IS NULL
                       THEN @Now + workflow_timeout_ms
                       ELSE workflow_deadline_epoch_ms
                   END
             WHERE workflow_uuid = @WorkflowId
            """;

        var updated = new List<string>();
        foreach (var id in dequeuedIds)
        {
            if (queue.RateLimit is not null && updated.Count + numRecentQueries >= queue.RateLimit.Limit)
                break;

            await connection.ExecuteAsync(new CommandDefinition(updateSql, new
            {
                Pending = WorkflowState.Pending.ToDbString(),
                AppVersion = appVersion,
                ExecutorId = executorId,
                Now = now,
                WorkflowId = id,
            }, transaction: tx, cancellationToken: ct)).ConfigureAwait(false);
            updated.Add(id);
        }

        if (updated.Count > 0)
            await tx.CommitAsync(ct).ConfigureAwait(false);
        else
            await tx.RollbackAsync(ct).ConfigureAwait(false);

        return updated;
    }
}
