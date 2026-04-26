namespace Dbos.Transact.Workflow;

/// <summary>
/// Represents a running or completed workflow. Port of Java's <c>WorkflowHandle&lt;T, E&gt;</c>.
/// </summary>
public abstract class WorkflowHandle<T>
{
    /// <summary>The unique ID of the workflow this handle refers to.</summary>
    public abstract string WorkflowId { get; }

    /// <summary>
    /// Returns the result of the workflow, blocking asynchronously until it is available.
    /// </summary>
    public abstract Task<T> GetResultAsync(CancellationToken ct = default);

    /// <summary>Returns the current status of the workflow, or <c>null</c> if unavailable.</summary>
    public abstract Task<WorkflowStatus?> GetStatusAsync(CancellationToken ct = default);
}
