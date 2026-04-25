using System.Globalization;
using System.Text.Json.Serialization;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class ListWorkflowsRequest : BaseMessage
{
    [JsonPropertyName("body")]
    public WorkflowsBody? Body { get; set; }

    public ListWorkflowsRequest() { Type = MessageType.ListWorkflows.GetValue(); }

    public sealed class WorkflowsBody
    {
        [JsonPropertyName("workflow_uuids")]
        public List<string>? WorkflowUuids { get; set; }

        [JsonPropertyName("workflow_name")]
        [JsonConverter(typeof(StringOrListConverter))]
        public List<string>? WorkflowName { get; set; }

        [JsonPropertyName("authenticated_user")]
        [JsonConverter(typeof(StringOrListConverter))]
        public List<string>? AuthenticatedUser { get; set; }

        [JsonPropertyName("start_time")]
        public string? StartTime { get; set; }

        [JsonPropertyName("end_time")]
        public string? EndTime { get; set; }

        [JsonPropertyName("status")]
        [JsonConverter(typeof(StringOrListConverter))]
        public List<string>? Status { get; set; }

        [JsonPropertyName("application_version")]
        [JsonConverter(typeof(StringOrListConverter))]
        public List<string>? ApplicationVersion { get; set; }

        [JsonPropertyName("forked_from")]
        [JsonConverter(typeof(StringOrListConverter))]
        public List<string>? ForkedFrom { get; set; }

        [JsonPropertyName("parent_workflow_id")]
        [JsonConverter(typeof(StringOrListConverter))]
        public List<string>? ParentWorkflowId { get; set; }

        [JsonPropertyName("queue_name")]
        [JsonConverter(typeof(StringOrListConverter))]
        public List<string>? QueueName { get; set; }

        [JsonPropertyName("workflow_id_prefix")]
        [JsonConverter(typeof(StringOrListConverter))]
        public List<string>? WorkflowIdPrefix { get; set; }

        [JsonPropertyName("executor_id")]
        [JsonConverter(typeof(StringOrListConverter))]
        public List<string>? ExecutorId { get; set; }

        [JsonPropertyName("limit")]
        public int? Limit { get; set; }

        [JsonPropertyName("offset")]
        public int? Offset { get; set; }

        [JsonPropertyName("sort_desc")]
        public bool? SortDesc { get; set; }

        [JsonPropertyName("load_input")]
        public bool? LoadInput { get; set; }

        [JsonPropertyName("load_output")]
        public bool? LoadOutput { get; set; }

        [JsonPropertyName("queues_only")]
        public bool? QueuesOnly { get; set; }

        [JsonPropertyName("was_forked_from")]
        public bool? WasForkedFrom { get; set; }

        [JsonPropertyName("has_parent")]
        public bool? HasParent { get; set; }
    }

    public ListWorkflowsInput AsInput()
    {
        ArgumentNullException.ThrowIfNull(Body);
        return new ListWorkflowsInput(
            Body.WorkflowUuids,
            Body.Status?.ConvertAll(Enum.Parse<WorkflowState>),
            Body.StartTime is not null ? DateTimeOffset.Parse(Body.StartTime, CultureInfo.InvariantCulture) : null,
            Body.EndTime is not null ? DateTimeOffset.Parse(Body.EndTime, CultureInfo.InvariantCulture) : null,
            Body.WorkflowName,
            null, null,
            Body.ApplicationVersion,
            Body.AuthenticatedUser,
            Body.Limit, Body.Offset, Body.SortDesc,
            Body.WorkflowIdPrefix,
            Body.LoadInput, Body.LoadOutput,
            Body.QueueName,
            Body.QueuesOnly,
            Body.ExecutorId,
            Body.ForkedFrom,
            Body.ParentWorkflowId,
            Body.WasForkedFrom,
            Body.HasParent);
    }
}
