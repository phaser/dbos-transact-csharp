using Dbos.Transact.Database;

namespace Dbos.Transact.Workflow.Internal;

/// <summary>
/// <see cref="WorkflowHandle{T}"/> that resolves by polling the system database.
/// Port of Java's <c>WorkflowHandleDBPoll</c>.
/// </summary>
public sealed class WorkflowHandleDbPoll<T> : WorkflowHandle<T>
{
    private readonly SystemDatabase _db;

    public WorkflowHandleDbPoll(SystemDatabase db, string workflowId)
    {
        _db = db;
        WorkflowId = workflowId;
    }

    public override string WorkflowId { get; }

    public override Task<T> GetResultAsync(CancellationToken ct = default) =>
        _db.AwaitWorkflowResultAsync<T>(WorkflowId, ct);

    public override Task<WorkflowStatus?> GetStatusAsync(CancellationToken ct = default) =>
        _db.GetWorkflowStatusAsync(WorkflowId, ct);
}
