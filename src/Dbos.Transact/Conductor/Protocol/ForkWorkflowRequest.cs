using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class ForkWorkflowRequest : BaseMessage
{
    [JsonPropertyName("body")]
    public ForkWorkflowBody? Body { get; set; }

    public ForkWorkflowRequest() { Type = MessageType.ForkWorkflow.GetValue(); }

    public ForkWorkflowRequest(string requestId, string workflowId, int startStep,
        string? appVer = null, string? newWorkflowId = null)
    {
        Type = MessageType.ForkWorkflow.GetValue();
        RequestId = requestId;
        Body = new ForkWorkflowBody
        {
            WorkflowId = workflowId,
            StartStep = startStep,
            ApplicationVersion = appVer,
            NewWorkflowId = newWorkflowId,
        };
    }

    public sealed class ForkWorkflowBody
    {
        [JsonPropertyName("workflow_id")]
        public string? WorkflowId { get; set; }

        [JsonPropertyName("start_step")]
        public int? StartStep { get; set; }

        [JsonPropertyName("application_version")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ApplicationVersion { get; set; }

        [JsonPropertyName("new_workflow_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? NewWorkflowId { get; set; }

        [JsonPropertyName("queue_name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? QueueName { get; set; }

        [JsonPropertyName("queue_partition_key")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? QueuePartitionKey { get; set; }
    }
}
