using System.Globalization;
using System.Text.Json.Serialization;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Admin;

public sealed record ListQueuedWorkflowsRequest(
    [property: JsonPropertyName("workflow_name")] string? WorkflowName,
    [property: JsonPropertyName("start_time")] string? StartTime,
    [property: JsonPropertyName("end_time")] string? EndTime,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("fork_from")] string? ForkFrom,
    [property: JsonPropertyName("parent_workflow_id")] string? ParentWorkflowId,
    [property: JsonPropertyName("queue_name")] string? QueueName,
    [property: JsonPropertyName("limit")] int? Limit,
    [property: JsonPropertyName("offset")] int? Offset,
    [property: JsonPropertyName("sort_desc")] bool? SortDesc,
    [property: JsonPropertyName("load_input")] bool? LoadInput)
{
    public ListWorkflowsInput AsInput() => new(
        null,
        Status is not null ? [Enum.Parse<WorkflowState>(Status)] : null,
        StartTime is not null ? DateTimeOffset.Parse(StartTime, CultureInfo.InvariantCulture) : null,
        EndTime is not null ? DateTimeOffset.Parse(EndTime, CultureInfo.InvariantCulture) : null,
        WorkflowName is not null ? [WorkflowName] : null,
        null,
        null,
        null,
        null,
        Limit,
        Offset,
        SortDesc,
        null,
        LoadInput,
        false,
        QueueName is not null ? [QueueName] : null,
        true,
        null,
        ForkFrom is not null ? [ForkFrom] : null,
        ParentWorkflowId is not null ? [ParentWorkflowId] : null,
        null,
        null);
}
