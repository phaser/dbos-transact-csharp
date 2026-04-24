namespace Dbos.Transact.Workflow;

/// <summary>
/// Options applied to synchronously-invoked workflows via the DBOS context.
/// Context-management methods (SetContext / Guard) are added in the context wave.
/// </summary>
public sealed record WorkflowOptions(
    string? WorkflowId,
    Timeout? Timeout,
    DateTimeOffset? Deadline)
{
    public string? WorkflowId { get; init; } = WorkflowId is { Length: 0 }
        ? throw new ArgumentException("WorkflowId must not be empty.", nameof(WorkflowId))
        : WorkflowId;

    public WorkflowOptions() : this(null, null, null) { }
    public WorkflowOptions(string? workflowId) : this(workflowId, null, null) { }

    public WorkflowOptions WithTimeout(TimeSpan timeout) => this with { Timeout = Workflow.Timeout.Of(timeout) };
    public WorkflowOptions WithNoTimeout() => this with { Timeout = new Timeout.None() };
}
