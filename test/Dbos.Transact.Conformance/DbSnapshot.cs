namespace Dbos.Transact.Conformance;

/// <summary>
/// Normalized snapshot of <c>dbos.*</c> rows for one workflow execution.
/// Timestamps, UUIDs, and executor IDs are stripped — only stable, comparable fields remain.
/// </summary>
public sealed record DbSnapshot(
    WorkflowRow Workflow,
    IReadOnlyList<StepRow> Steps
);

/// <summary>Normalized row from <c>dbos.workflow_status</c>.</summary>
public sealed record WorkflowRow(
    string Status,
    string? OutputJson,
    string? ErrorJson,
    int RecoveryAttempts
);

/// <summary>Normalized row from <c>dbos.operation_outputs</c>, ordered by <c>function_id</c>.</summary>
public sealed record StepRow(
    int FunctionId,
    string FunctionName,
    string? OutputJson,
    string? ErrorJson
);
