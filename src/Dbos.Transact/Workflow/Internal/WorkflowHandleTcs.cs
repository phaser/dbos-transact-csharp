using Dbos.Transact.Database;

namespace Dbos.Transact.Workflow.Internal;

/// <summary>
/// <see cref="WorkflowHandle{T}"/> backed by a <see cref="TaskCompletionSource{T}"/>.
/// Used when the executor holds a live in-process reference to the running workflow.
/// Port of Java's <c>WorkflowHandleFuture</c>.
/// </summary>
public sealed class WorkflowHandleTcs<T> : WorkflowHandle<T>
{
    private readonly TaskCompletionSource<T> _tcs;
    private readonly SystemDatabase? _db;

    public WorkflowHandleTcs(string workflowId, TaskCompletionSource<T> tcs, SystemDatabase? db = null)
    {
        WorkflowId = workflowId;
        _tcs = tcs;
        _db = db;
    }

    public override string WorkflowId { get; }

    public override Task<T> GetResultAsync(CancellationToken ct = default) =>
        _tcs.Task.WaitAsync(ct);

    public override Task<WorkflowStatus?> GetStatusAsync(CancellationToken ct = default) =>
        _db?.GetWorkflowStatusAsync(WorkflowId, ct) ?? Task.FromResult<WorkflowStatus?>(null);
}
