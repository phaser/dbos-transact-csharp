using Dbos.Transact.Workflow;
using Timeout = Dbos.Transact.Workflow.Timeout;

namespace Dbos.Transact;

/// <summary>
/// Options for starting a workflow: idempotency ID, timeout/deadline, queue placement, and delay.
/// </summary>
public sealed record StartWorkflowOptions(
    string? WorkflowId,
    Timeout? Timeout,
    DateTimeOffset? Deadline,
    string? QueueName,
    string? DeduplicationId,
    int? Priority,
    string? QueuePartitionKey,
    TimeSpan? Delay,
    string? AppVersion)
{
    public string? WorkflowId { get; init; } = WorkflowId is { Length: 0 }
        ? throw new ArgumentException("WorkflowId must not be empty.", nameof(WorkflowId))
        : WorkflowId;

    public string? QueueName { get; init; } = QueueName is { Length: 0 }
        ? throw new ArgumentException("QueueName must not be empty.", nameof(QueueName))
        : QueueName;

    public string? DeduplicationId { get; init; } = DeduplicationId is { Length: 0 }
        ? throw new ArgumentException("DeduplicationId must not be empty.", nameof(DeduplicationId))
        : DeduplicationId;

    public string? QueuePartitionKey { get; init; } = QueuePartitionKey is { Length: 0 }
        ? throw new ArgumentException("QueuePartitionKey must not be empty.", nameof(QueuePartitionKey))
        : QueuePartitionKey;

    public TimeSpan? Delay { get; init; } = Delay.HasValue && Delay.Value <= TimeSpan.Zero
        ? throw new ArgumentOutOfRangeException(nameof(Delay), "Delay must be a positive non-zero duration.")
        : Delay;

    public string? AppVersion { get; init; } = AppVersion is { Length: 0 }
        ? throw new ArgumentException("AppVersion must not be empty.", nameof(AppVersion))
        : AppVersion;

    public StartWorkflowOptions() : this(null, null, null, null, null, null, null, null, null) { }
    public StartWorkflowOptions(string workflowId) : this(workflowId, null, null, null, null, null, null, null, null) { }
    public StartWorkflowOptions(Queue queue) : this(null, null, null, queue.Name, null, null, null, null, null) { }

    // Timeout positivity is enforced by Timeout.Explicit itself.

    public StartWorkflowOptions WithTimeout(TimeSpan timeout) => this with { Timeout = Workflow.Timeout.Of(timeout) };
    public StartWorkflowOptions WithNoTimeout() => this with { Timeout = new Timeout.None() };
    public StartWorkflowOptions WithQueue(Queue queue) => this with { QueueName = queue.Name };
}
