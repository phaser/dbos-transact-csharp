using System.Globalization;
using System.Text.Json.Serialization;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class GetWorkflowAggregatesRequest : BaseMessage
{
    [JsonPropertyName("body")]
    public AggregatesBody? Body { get; set; }

    public GetWorkflowAggregatesRequest() { Type = MessageType.GetWorkflowAggregates.GetValue(); }

    public sealed class AggregatesBody
    {
        [JsonPropertyName("group_by_status")]
        public bool GroupByStatus { get; set; }

        [JsonPropertyName("group_by_name")]
        public bool GroupByName { get; set; }

        [JsonPropertyName("group_by_queue_name")]
        public bool GroupByQueueName { get; set; }

        [JsonPropertyName("group_by_executor_id")]
        public bool GroupByExecutorId { get; set; }

        [JsonPropertyName("group_by_application_version")]
        public bool GroupByApplicationVersion { get; set; }

        [JsonPropertyName("status")]
        public List<string>? Status { get; set; }

        [JsonPropertyName("start_time")]
        public string? StartTime { get; set; }

        [JsonPropertyName("end_time")]
        public string? EndTime { get; set; }

        [JsonPropertyName("name")]
        public List<string>? Name { get; set; }

        [JsonPropertyName("app_version")]
        public List<string>? AppVersion { get; set; }

        [JsonPropertyName("executor_id")]
        public List<string>? ExecutorId { get; set; }

        [JsonPropertyName("queue_name")]
        public List<string>? QueueName { get; set; }

        [JsonPropertyName("workflow_id_prefix")]
        public List<string>? WorkflowIdPrefix { get; set; }
    }

    public GetWorkflowAggregatesInput ToInput()
    {
        if (Body is null) return new GetWorkflowAggregatesInput();
        return new GetWorkflowAggregatesInput(
            Body.GroupByStatus,
            Body.GroupByName,
            Body.GroupByQueueName,
            Body.GroupByExecutorId,
            Body.GroupByApplicationVersion,
            Body.Name,
            Body.Status,
            Body.QueueName,
            Body.ExecutorId,
            Body.AppVersion,
            Body.WorkflowIdPrefix,
            Body.StartTime is not null ? DateTimeOffset.Parse(Body.StartTime, CultureInfo.InvariantCulture) : null,
            Body.EndTime is not null ? DateTimeOffset.Parse(Body.EndTime, CultureInfo.InvariantCulture) : null);
    }
}
