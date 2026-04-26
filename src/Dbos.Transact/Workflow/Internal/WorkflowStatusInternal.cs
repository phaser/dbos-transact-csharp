namespace Dbos.Transact.Workflow.Internal;

/// <summary>
/// Internal wire-level representation of a workflow's persistent status row.
/// Port of Java's <c>WorkflowStatusInternal</c> record.
/// </summary>
public sealed record WorkflowStatusInternal(
    string WorkflowId,
    string WorkflowName,
    string ClassName,
    string? InstanceName,
    string? QueueName,
    string? DeduplicationId,
    int? Priority,
    string? QueuePartitionKey,
    TimeSpan? Delay,
    string? AuthenticatedUser,
    string? AssumedRole,
    string[]? AuthenticatedRoles,
    string? Inputs,
    string? ExecutorId,
    string? AppVersion,
    string? AppId,
    TimeSpan? Timeout,
    DateTimeOffset? Deadline,
    string? ParentWorkflowId,
    string? Serialization)
{
    public long? TimeoutMs => Timeout.HasValue ? (long)Timeout.Value.TotalMilliseconds : null;
    public long? DelayMs => Delay.HasValue ? (long)Delay.Value.TotalMilliseconds : null;
    public long? DeadlineEpochMs => Deadline.HasValue ? Deadline.Value.ToUnixTimeMilliseconds() : null;
}
