using Dbos.Transact.Workflow;

namespace Dbos.Transact.Database;

/// <summary>
/// Result returned by <c>SystemDatabase.InitWorkflowStatus</c>.
/// Port of Java's <c>WorkflowInitResult</c> record.
/// </summary>
public sealed record WorkflowInitResult(
    WorkflowState Status,
    long? DeadlineEpochMs,
    bool ShouldExecuteOnThisExecutor,
    string? Serialization);
