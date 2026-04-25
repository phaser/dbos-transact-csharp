namespace Dbos.Transact.Workflow;

public sealed record ListWorkflowsInput(
    IReadOnlyList<string>? WorkflowIds,
    IReadOnlyList<WorkflowState>? Status,
    DateTimeOffset? StartTime,
    DateTimeOffset? EndTime,
    IReadOnlyList<string>? WorkflowName,
    string? ClassName,
    string? InstanceName,
    IReadOnlyList<string>? ApplicationVersion,
    IReadOnlyList<string>? AuthenticatedUser,
    int? Limit,
    int? Offset,
    bool? SortDesc,
    IReadOnlyList<string>? WorkflowIdPrefix,
    bool? LoadInput,
    bool? LoadOutput,
    IReadOnlyList<string>? QueueName,
    bool? QueuesOnly,
    IReadOnlyList<string>? ExecutorIds,
    IReadOnlyList<string>? ForkedFrom,
    IReadOnlyList<string>? ParentWorkflowId,
    bool? WasForkedFrom,
    bool? HasParent)
{
    public ListWorkflowsInput() : this(
        null, null, null, null, null, null, null, null, null,
        null, null, null, null, null, null, null, null, null,
        null, null, null, null)
    {
    }

    public ListWorkflowsInput(string workflowId) : this([workflowId]) { }

    public ListWorkflowsInput(IReadOnlyList<string> workflowIds) : this(
        workflowIds, null, null, null, null, null, null, null, null,
        null, null, null, null, null, null, null, null, null,
        null, null, null, null)
    {
    }
}
