namespace Dbos.Transact.Workflow;

/// <summary>
/// Options for forking a workflow: assigned ID, app version, timeout, and queue placement.
/// </summary>
public sealed record ForkOptions(
    string? ForkedWorkflowId,
    string? ApplicationVersion,
    Timeout? Timeout,
    string? QueueName,
    string? QueuePartitionKey)
{
    public ForkOptions() : this(null, null, null, null, null) { }
    public ForkOptions(string forkedWorkflowId) : this(forkedWorkflowId, null, null, null, null) { }

    // Timeout positivity is enforced by Timeout.Explicit itself.

    public ForkOptions WithTimeout(TimeSpan timeout) => this with { Timeout = Workflow.Timeout.Of(timeout) };
    public ForkOptions WithNoTimeout() => this with { Timeout = new Timeout.None() };
}
