namespace Dbos.Transact.Workflow;

public sealed record GetWorkflowAggregatesInput(
    bool GroupByStatus,
    bool GroupByName,
    bool GroupByQueueName,
    bool GroupByExecutorId,
    bool GroupByApplicationVersion,
    IReadOnlyList<string>? WorkflowName,
    IReadOnlyList<string>? Status,
    IReadOnlyList<string>? QueueName,
    IReadOnlyList<string>? ExecutorIds,
    IReadOnlyList<string>? ApplicationVersion,
    IReadOnlyList<string>? WorkflowIdPrefix,
    DateTimeOffset? StartTime,
    DateTimeOffset? EndTime)
{
    public GetWorkflowAggregatesInput() : this(
        false, false, false, false, false,
        null, null, null, null, null, null, null, null)
    {
    }
}
