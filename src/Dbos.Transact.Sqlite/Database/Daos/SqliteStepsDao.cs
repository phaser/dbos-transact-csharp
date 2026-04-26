using System.Data;
using System.Data.Common;
using Dapper;
using Dbos.Transact.Database.Daos;
using Dbos.Transact.Exceptions;
using Dbos.Transact.Workflow;
using Dbos.Transact.Workflow.Internal;

namespace Dbos.Transact.Sqlite.Database.Daos;

/// <summary>SQLite-backed implementation of <see cref="StepsDao"/>.</summary>
public sealed class SqliteStepsDao : StepsDao
{
    private readonly Func<DbConnection> _connectionFactory;

    public SqliteStepsDao(Func<DbConnection> connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public override async Task<StepResult> CheckStepExecutionTxnAsync(
        IDbConnection connection,
        string workflowId,
        int functionId,
        string functionName,
        CancellationToken ct = default)
    {
        var status = await connection.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(
                "SELECT status FROM workflow_status WHERE workflow_uuid = @WorkflowId",
                new { WorkflowId = workflowId }, cancellationToken: ct));

        if (status is null)
            throw new DbosNonExistentWorkflowException(workflowId);

        if (string.Equals(status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
            throw new DbosWorkflowCancelledException($"Workflow {workflowId} is cancelled. Aborting step.");

        var row = await connection.QuerySingleOrDefaultAsync<StepOutputRow>(
            new CommandDefinition(
                "SELECT output, error, function_name, serialization FROM operation_outputs WHERE workflow_uuid = @WorkflowId AND function_id = @FunctionId",
                new { WorkflowId = workflowId, FunctionId = functionId }, cancellationToken: ct));

        if (row is null)
            return new StepResult(workflowId, functionId, functionName);

        if (!string.Equals(functionName, row.FunctionName, StringComparison.Ordinal))
            throw new DbosUnexpectedStepException(workflowId, functionId, functionName, row.FunctionName ?? string.Empty);

        return new StepResult(workflowId, functionId, row.FunctionName ?? functionName,
            Output: row.Output, Error: row.Error, Serialization: row.Serialization);
    }

    public override async Task RecordStepResultTxnAsync(
        IDbConnection connection,
        StepResult result,
        long startTimeEpochMs,
        long endTimeEpochMs,
        CancellationToken ct = default)
    {
        const string sql = """
            INSERT OR IGNORE INTO operation_outputs
                (workflow_uuid, function_id, function_name, output, error, child_workflow_id, started_at_epoch_ms, completed_at_epoch_ms, serialization)
            VALUES
                (@WorkflowId, @FunctionId, @FunctionName, @Output, @Error, @ChildWorkflowId, @StartedAt, @CompletedAt, @Serialization)
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            WorkflowId = result.WorkflowId,
            FunctionId = result.StepId,
            FunctionName = result.FunctionName,
            Output = result.Output,
            Error = result.Error,
            ChildWorkflowId = result.ChildWorkflowId,
            StartedAt = startTimeEpochMs,
            CompletedAt = endTimeEpochMs,
            Serialization = result.Serialization,
        }, cancellationToken: ct));
    }

    public override async Task<IReadOnlyList<StepInfo>> ListWorkflowStepsAsync(
        string workflowId, bool loadOutput, int? limit, int? offset, CancellationToken ct = default)
    {
        var sql = new System.Text.StringBuilder("""
            SELECT function_id, function_name, output, error, child_workflow_id, started_at_epoch_ms, serialization
            FROM operation_outputs
            WHERE workflow_uuid = @WorkflowId
            ORDER BY function_id
            """);
        var parameters = new DynamicParameters();
        parameters.Add("WorkflowId", workflowId);

        if (limit.HasValue) { sql.Append(" LIMIT @Limit"); parameters.Add("Limit", limit.Value); }
        if (offset.HasValue) { sql.Append(" OFFSET @Offset"); parameters.Add("Offset", offset.Value); }

        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct);
        var rows = await connection.QueryAsync<StepOutputRow>(
            new CommandDefinition(sql.ToString(), parameters, cancellationToken: ct));

        return rows.Select(r => new StepInfo(
            r.FunctionId ?? 0,
            r.FunctionName ?? string.Empty,
            loadOutput ? r.Output : null,
            r.Error is not null ? new ErrorResult(null, null, r.Error, r.Serialization, null) : null,
            r.ChildWorkflowId,
            r.StartedAt.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(r.StartedAt.Value) : null,
            null,
            r.Serialization)).ToList();
    }

    public override async Task SleepAsync(string workflowId, int functionId, TimeSpan duration, CancellationToken ct = default)
    {
        const string sql = """
            INSERT OR IGNORE INTO operation_outputs
                (workflow_uuid, function_id, function_name, started_at_epoch_ms, completed_at_epoch_ms)
            VALUES
                (@WorkflowId, @FunctionId, 'DBOS.sleep', @StartedAt, @CompletedAt)
            """;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            WorkflowId = workflowId,
            FunctionId = functionId,
            StartedAt = now,
            CompletedAt = now + (long)duration.TotalMilliseconds,
        }, cancellationToken: ct));
    }

    private sealed class StepOutputRow
    {
        public int? FunctionId { get; set; }
        public string? FunctionName { get; set; }
        public string? Output { get; set; }
        public string? Error { get; set; }
        public string? ChildWorkflowId { get; set; }
        public long? StartedAt { get; set; }
        public string? Serialization { get; set; }
    }
}
