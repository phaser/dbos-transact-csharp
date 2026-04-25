using System.Globalization;
using System.Text.Json.Serialization;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Admin;

public sealed record ListWorkflowsRequest(
    [property: JsonPropertyName("workflow_uuids")] IReadOnlyList<string>? WorkflowUuids,
    [property: JsonPropertyName("workflow_name")] string? WorkflowName,
    [property: JsonPropertyName("authenticated_user")] string? AuthenticatedUser,
    [property: JsonPropertyName("start_time")] string? StartTime,
    [property: JsonPropertyName("end_time")] string? EndTime,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("application_version")] string? ApplicationVersion,
    [property: JsonPropertyName("fork_from")] string? ForkFrom,
    [property: JsonPropertyName("parent_workflow_id")] string? ParentWorkflowId,
    [property: JsonPropertyName("limit")] int? Limit,
    [property: JsonPropertyName("offset")] int? Offset,
    [property: JsonPropertyName("sort_desc")] bool? SortDesc,
    [property: JsonPropertyName("workflow_id_prefix")] string? WorkflowIdPrefix,
    [property: JsonPropertyName("load_input")] bool? LoadInput,
    [property: JsonPropertyName("load_output")] bool? LoadOutput)
{
    public ListWorkflowsInput AsInput() => new(
        WorkflowUuids,
        Status is not null ? [Enum.Parse<WorkflowState>(Status)] : null,
        StartTime is not null ? DateTimeOffset.Parse(StartTime, CultureInfo.InvariantCulture) : null,
        EndTime is not null ? DateTimeOffset.Parse(EndTime, CultureInfo.InvariantCulture) : null,
        WorkflowName is not null ? [WorkflowName] : null,
        null,
        null,
        ApplicationVersion is not null ? [ApplicationVersion] : null,
        AuthenticatedUser is not null ? [AuthenticatedUser] : null,
        Limit,
        Offset,
        SortDesc,
        WorkflowIdPrefix is not null ? [WorkflowIdPrefix] : null,
        LoadInput,
        LoadOutput,
        null,
        false,
        null,
        ForkFrom is not null ? [ForkFrom] : null,
        ParentWorkflowId is not null ? [ParentWorkflowId] : null,
        null,
        null);
}
