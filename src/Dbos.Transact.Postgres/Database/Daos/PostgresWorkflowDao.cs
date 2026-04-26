using System.Data;
using System.Data.Common;
using Dapper;
using Dbos.Transact.Database;
using Dbos.Transact.Database.Daos;
using Dbos.Transact.Exceptions;
using Dbos.Transact.Workflow;
using Dbos.Transact.Workflow.Internal;

namespace Dbos.Transact.Postgres.Database.Daos;

/// <summary>
/// PostgreSQL-backed implementation of <see cref="WorkflowDao"/>.
/// </summary>
public sealed class PostgresWorkflowDao : WorkflowDao
{
    private readonly Func<DbConnection> _connectionFactory;
    private readonly string _schema;
    private readonly string _schemaPrefix;

    public PostgresWorkflowDao(Func<DbConnection> connectionFactory, string schema)
    {
        _connectionFactory = connectionFactory;
        _schema = schema;
        _schemaPrefix = string.IsNullOrEmpty(schema) ? string.Empty : $"\"{schema}\".";
    }

    // ── Internal DTO used for Dapper result mapping ───────────────────────────

    private sealed class WorkflowStatusRow
    {
        public string? WorkflowId { get; set; }
        public string? Status { get; set; }
        public string? WorkflowName { get; set; }
        public string? ClassName { get; set; }
        public string? InstanceName { get; set; }
        public string? AuthenticatedUser { get; set; }
        public string? AssumedRole { get; set; }
        public string? AuthenticatedRoles { get; set; }
        public string? Inputs { get; set; }
        public string? Output { get; set; }
        public string? Error { get; set; }
        public string? ExecutorId { get; set; }
        public long? CreatedAt { get; set; }
        public long? UpdatedAt { get; set; }
        public string? AppVersion { get; set; }
        public string? AppId { get; set; }
        public int? RecoveryAttempts { get; set; }
        public string? QueueName { get; set; }
        public long? TimeoutMs { get; set; }
        public long? DeadlineEpochMs { get; set; }
        public long? StartedAt { get; set; }
        public string? DeduplicationId { get; set; }
        public int? Priority { get; set; }
        public string? QueuePartitionKey { get; set; }
        public string? ForkedFrom { get; set; }
        public string? ParentWorkflowId { get; set; }
        public bool? WasForkedFrom { get; set; }
        public long? DelayUntilEpochMs { get; set; }
        public string? Serialization { get; set; }

        public WorkflowStatus ToWorkflowStatus() => new(
            WorkflowId: WorkflowId,
            Status: Status is not null && Enum.TryParse<WorkflowState>(Status, out var state) ? state : null,
            WorkflowName: WorkflowName,
            ClassName: ClassName,
            InstanceName: InstanceName,
            AuthenticatedUser: AuthenticatedUser,
            AssumedRole: AssumedRole,
            AuthenticatedRoles: AuthenticatedRoles?.Split(',', StringSplitOptions.RemoveEmptyEntries),
            Input: null, // Inputs is stored serialized; deserialization is caller's responsibility
            Output: Output,
            Error: null, // Error deserialization is caller's responsibility
            ExecutorId: ExecutorId,
            CreatedAt: CreatedAt.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(CreatedAt.Value) : null,
            UpdatedAt: UpdatedAt.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(UpdatedAt.Value) : null,
            AppVersion: AppVersion,
            AppId: AppId,
            RecoveryAttempts: RecoveryAttempts,
            QueueName: QueueName,
            Timeout: TimeoutMs.HasValue ? TimeSpan.FromMilliseconds(TimeoutMs.Value) : null,
            Deadline: DeadlineEpochMs.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(DeadlineEpochMs.Value) : null,
            StartedAt: StartedAt.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(StartedAt.Value) : null,
            DeduplicationId: DeduplicationId,
            Priority: Priority,
            QueuePartitionKey: QueuePartitionKey,
            ForkedFrom: ForkedFrom,
            ParentWorkflowId: ParentWorkflowId,
            WasForkedFrom: WasForkedFrom,
            DelayUntil: DelayUntilEpochMs.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(DelayUntilEpochMs.Value) : null,
            Serialization: Serialization);
    }

    private const string WorkflowStatusColumns = """
        workflow_uuid AS WorkflowId,
        status AS Status,
        name AS WorkflowName,
        class_name AS ClassName,
        config_name AS InstanceName,
        authenticated_user AS AuthenticatedUser,
        assumed_role AS AssumedRole,
        authenticated_roles AS AuthenticatedRoles,
        inputs AS Inputs,
        output AS Output,
        error AS Error,
        executor_id AS ExecutorId,
        created_at AS CreatedAt,
        updated_at AS UpdatedAt,
        application_version AS AppVersion,
        application_id AS AppId,
        recovery_attempts AS RecoveryAttempts,
        queue_name AS QueueName,
        workflow_timeout_ms AS TimeoutMs,
        workflow_deadline_epoch_ms AS DeadlineEpochMs,
        started_at_epoch_ms AS StartedAt,
        deduplication_id AS DeduplicationId,
        priority AS Priority,
        queue_partition_key AS QueuePartitionKey,
        forked_from AS ForkedFrom,
        parent_workflow_id AS ParentWorkflowId,
        was_forked_from AS WasForkedFrom,
        delay_until_epoch_ms AS DelayUntilEpochMs,
        serialization AS Serialization
        """;

    // ── Core operations ───────────────────────────────────────────────────────

    public override async Task<WorkflowStatus?> GetWorkflowStatusAsync(string workflowId, CancellationToken ct = default)
    {
        var sql = $"""
            SELECT {WorkflowStatusColumns}
            FROM {_schemaPrefix}workflow_status
            WHERE workflow_uuid = @WorkflowId
            """;

        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<WorkflowStatusRow>(
            new CommandDefinition(sql, new { WorkflowId = workflowId }, cancellationToken: ct));
        return row?.ToWorkflowStatus();
    }

    public override async Task<string?> GetWorkflowSerializationAsync(string workflowId, CancellationToken ct = default)
    {
        var sql = $"""
            SELECT serialization FROM {_schemaPrefix}workflow_status WHERE workflow_uuid = @WorkflowId
            """;
        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(sql, new { WorkflowId = workflowId }, cancellationToken: ct));
    }

    public override async Task RecordWorkflowOutputAsync(string workflowId, string? result, CancellationToken ct = default)
    {
        var sql = $"""
            UPDATE {_schemaPrefix}workflow_status
            SET output = @Output, status = 'SUCCESS',
                updated_at = (EXTRACT(epoch FROM now()) * 1000::numeric)::bigint
            WHERE workflow_uuid = @WorkflowId
            """;
        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct);
        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { WorkflowId = workflowId, Output = result }, cancellationToken: ct));
    }

    public override async Task RecordWorkflowErrorAsync(string workflowId, string? errorPayload, CancellationToken ct = default)
    {
        var sql = $"""
            UPDATE {_schemaPrefix}workflow_status
            SET error = @Error, status = 'ERROR',
                updated_at = (EXTRACT(epoch FROM now()) * 1000::numeric)::bigint
            WHERE workflow_uuid = @WorkflowId
            """;
        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct);
        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { WorkflowId = workflowId, Error = errorPayload }, cancellationToken: ct));
    }

    public override async Task<IReadOnlyList<WorkflowStatus>> ListWorkflowsAsync(ListWorkflowsInput input, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct);

        var sql = new System.Text.StringBuilder($"SELECT {WorkflowStatusColumns} FROM {_schemaPrefix}workflow_status WHERE 1=1");
        var parameters = new DynamicParameters();

        if (input.WorkflowIds?.Count > 0)
        {
            sql.Append(" AND workflow_uuid = ANY(@WorkflowIds)");
            parameters.Add("WorkflowIds", input.WorkflowIds.ToArray());
        }
        if (input.WorkflowName?.Count > 0)
        {
            sql.Append(" AND name = ANY(@WorkflowName)");
            parameters.Add("WorkflowName", input.WorkflowName.ToArray());
        }
        if (input.Status?.Count > 0)
        {
            sql.Append(" AND status = ANY(@Status)");
            parameters.Add("Status", input.Status.Select(s => s.ToString()).ToArray());
        }
        if (input.StartTime.HasValue)
        {
            sql.Append(" AND created_at >= @StartTime");
            parameters.Add("StartTime", input.StartTime.Value.ToUnixTimeMilliseconds());
        }
        if (input.EndTime.HasValue)
        {
            sql.Append(" AND created_at <= @EndTime");
            parameters.Add("EndTime", input.EndTime.Value.ToUnixTimeMilliseconds());
        }
        if (input.QueueName?.Count > 0)
        {
            sql.Append(" AND queue_name = ANY(@QueueName)");
            parameters.Add("QueueName", input.QueueName.ToArray());
        }

        sql.Append(input.SortDesc == false ? " ORDER BY created_at ASC" : " ORDER BY created_at DESC");

        if (input.Limit.HasValue)
        {
            sql.Append(" LIMIT @Limit");
            parameters.Add("Limit", input.Limit.Value);
        }
        if (input.Offset.HasValue)
        {
            sql.Append(" OFFSET @Offset");
            parameters.Add("Offset", input.Offset.Value);
        }

        var rows = await connection.QueryAsync<WorkflowStatusRow>(
            new CommandDefinition(sql.ToString(), parameters, cancellationToken: ct));
        return rows.Select(r => r.ToWorkflowStatus()).ToList();
    }

    public override async Task<IReadOnlyList<WorkflowStatus>> GetPendingWorkflowsAsync(
        IReadOnlyList<string> executorIds, string? appVersion, CancellationToken ct = default)
    {
        var sql = $"""
            SELECT {WorkflowStatusColumns}
            FROM {_schemaPrefix}workflow_status
            WHERE status = 'PENDING'
              AND executor_id = ANY(@ExecutorIds)
            """;
        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct);
        var rows = await connection.QueryAsync<WorkflowStatusRow>(
            new CommandDefinition(sql, new { ExecutorIds = executorIds.ToArray() }, cancellationToken: ct));
        return rows.Select(r => r.ToWorkflowStatus()).ToList();
    }

    public override async Task CancelWorkflowsAsync(IReadOnlyList<string> workflowIds, CancellationToken ct = default)
    {
        var sql = $"""
            UPDATE {_schemaPrefix}workflow_status
            SET status = 'CANCELLED',
                updated_at = (EXTRACT(epoch FROM now()) * 1000::numeric)::bigint
            WHERE workflow_uuid = ANY(@WorkflowIds)
              AND status IN ('PENDING', 'ENQUEUED', 'DELAYED')
            """;
        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct);
        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { WorkflowIds = workflowIds.ToArray() }, cancellationToken: ct));
    }

    public override async Task ResumeWorkflowsAsync(IReadOnlyList<string> workflowIds, string? queueName, CancellationToken ct = default)
    {
        var sql = $"""
            UPDATE {_schemaPrefix}workflow_status
            SET status = CASE WHEN queue_name IS NOT NULL THEN 'ENQUEUED' ELSE 'PENDING' END,
                queue_name = COALESCE(@QueueName, queue_name),
                updated_at = (EXTRACT(epoch FROM now()) * 1000::numeric)::bigint
            WHERE workflow_uuid = ANY(@WorkflowIds)
              AND status = 'CANCELLED'
            """;
        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct);
        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { WorkflowIds = workflowIds.ToArray(), QueueName = queueName }, cancellationToken: ct));
    }

    // ── Stubbed operations (implemented in future waves) ─────────────────────

    public override Task<WorkflowInitResult> InitWorkflowStatusAsync(
        WorkflowStatusInternal initStatus, int maxRetries, bool isRecoveryRequest, bool isDequeuedRequest, CancellationToken ct = default) =>
        throw new NotImplementedException("DBOS-20: InitWorkflowStatus requires full transaction support");

    public override Task<IReadOnlyList<WorkflowAggregateRow>> GetWorkflowAggregatesAsync(GetWorkflowAggregatesInput input, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public override Task RecordChildWorkflowAsync(IDbConnection connection, string workflowId, int functionId, string childWorkflowId, string? serialization, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public override Task<string?> CheckChildWorkflowAsync(string workflowId, int functionId, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public override Task<T> AwaitWorkflowResultAsync<T>(string workflowId, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public override Task DeleteWorkflowsAsync(IReadOnlyList<string> workflowIds, bool deleteChildren, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public override Task<string> ForkWorkflowAsync(string originalWorkflowId, int startStep, ForkOptions options, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
