namespace Dbos.Transact.Exceptions;

/// <summary>
/// Thrown when an attempt is made to enqueue a workflow into a queue with deduplication and a
/// queued workflow with this <c>DeduplicationId</c> already exists.
/// </summary>
public sealed class DbosQueueDuplicatedException : DbosException
{
    public string WorkflowId { get; }
    public string QueueName { get; }
    public string DeduplicationId { get; }

    public DbosQueueDuplicatedException(string workflowId, string queueName, string deduplicationId)
        : base($"Workflow {workflowId} (Queue: {queueName}, Deduplication ID: {deduplicationId}) is already enqueued.")
    {
        WorkflowId = workflowId;
        QueueName = queueName;
        DeduplicationId = deduplicationId;
    }
}
