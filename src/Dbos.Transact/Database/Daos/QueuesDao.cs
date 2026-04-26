using Dbos.Transact.Workflow;

namespace Dbos.Transact.Database.Daos;

/// <summary>
/// Data-access methods for the <c>workflow_queues</c> table.
/// Port of Java's <c>QueuesDAO</c>. Dialect-specific SQL is provided by subclasses.
/// </summary>
public abstract class QueuesDao
{
    public abstract Task<bool> ClearQueueAssignmentAsync(string workflowId, CancellationToken ct = default);

    public abstract Task<IReadOnlyList<string>> GetQueuePartitionsAsync(string queueName, CancellationToken ct = default);

    public abstract Task<IReadOnlyList<string>> GetAndStartQueuedWorkflowsAsync(
        Queue queue,
        string executorId,
        string? appVersion,
        string? partitionKey,
        CancellationToken ct = default);
}
