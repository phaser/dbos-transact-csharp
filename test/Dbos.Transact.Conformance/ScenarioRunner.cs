using Dbos.Transact.Execution;
using Dbos.Transact.Json;
using Dbos.Transact.Postgres.Database;
using Npgsql;

namespace Dbos.Transact.Conformance;

/// <summary>
/// Executes a <see cref="ConformanceScenario"/> against a Postgres instance,
/// waits for the workflow to complete, and captures a normalized <see cref="DbSnapshot"/>.
/// </summary>
public sealed class ScenarioRunner(string connectionString, string schema)
{
    private const int DefaultTimeoutSeconds = 30;

    /// <summary>
    /// Runs <paramref name="scenario"/>, waits for completion, and returns the result with a DB snapshot.
    /// </summary>
    public async Task<ScenarioRunResult> RunAsync(
        ConformanceScenario scenario,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(DefaultTimeoutSeconds));

        await using var db = new PostgresSystemDatabase(connectionString, schema);
        await using var executor = new DbosExecutor(
            db,
            DbosJsonSerializer.Instance,
            executorId: $"conformance-{Guid.NewGuid():N}");

        var workflowId = await scenario.RunAsync(executor, timeoutCts.Token);
        var snapshot = await CaptureSnapshotAsync(workflowId, timeoutCts.Token);
        return new ScenarioRunResult(workflowId, snapshot);
    }

    /// <summary>
    /// Queries the DB and returns a normalized snapshot of workflow_status + operation_outputs
    /// for <paramref name="workflowId"/>.
    /// </summary>
    public async Task<DbSnapshot> CaptureSnapshotAsync(
        string workflowId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        var workflowRow = await QueryWorkflowRowAsync(conn, workflowId, cancellationToken);
        var steps = await QueryStepsAsync(conn, workflowId, cancellationToken);
        return new DbSnapshot(workflowRow, steps);
    }

    /// <summary>
    /// Directly corrupts a <c>workflow_status</c> row for divergence-injection testing.
    /// </summary>
    public async Task InjectDivergenceAsync(
        string workflowId,
        string corruptStatus,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            UPDATE "{schema}".workflow_status
            SET status = @status
            WHERE workflow_uuid = @id
            """;
        cmd.Parameters.AddWithValue("status", corruptStatus);
        cmd.Parameters.AddWithValue("id", workflowId);
        int rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
        if (rows == 0)
            throw new InvalidOperationException($"No workflow_status row found for id '{workflowId}'");
    }

    private async Task<WorkflowRow> QueryWorkflowRowAsync(
        NpgsqlConnection conn,
        string workflowId,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT status, output, error, recovery_attempts
            FROM "{schema}".workflow_status
            WHERE workflow_uuid = @id
            """;
        cmd.Parameters.AddWithValue("id", workflowId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new InvalidOperationException($"No workflow_status row found for id '{workflowId}'");

        return new WorkflowRow(
            Status: reader.GetString(0),
            OutputJson: reader.IsDBNull(1) ? null : reader.GetString(1),
            ErrorJson: reader.IsDBNull(2) ? null : reader.GetString(2),
            RecoveryAttempts: (int)reader.GetInt64(3));
    }

    private async Task<IReadOnlyList<StepRow>> QueryStepsAsync(
        NpgsqlConnection conn,
        string workflowId,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT function_id, function_name, output, error
            FROM "{schema}".operation_outputs
            WHERE workflow_uuid = @id
            ORDER BY function_id
            """;
        cmd.Parameters.AddWithValue("id", workflowId);

        var steps = new List<StepRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            steps.Add(new StepRow(
                FunctionId: reader.GetInt32(0),
                FunctionName: reader.GetString(1),
                OutputJson: reader.IsDBNull(2) ? null : reader.GetString(2),
                ErrorJson: reader.IsDBNull(3) ? null : reader.GetString(3)));
        }
        return steps;
    }
}

/// <summary>Result of running a conformance scenario.</summary>
public sealed record ScenarioRunResult(string WorkflowId, DbSnapshot Snapshot);
