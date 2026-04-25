using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class ResumeRequest : BaseMessage
{
    [JsonPropertyName("workflow_id")]
    public string? WorkflowId { get; set; }

    [JsonPropertyName("workflow_ids")]
    public List<string>? WorkflowIds { get; set; }

    [JsonPropertyName("queue_name")]
    public string? QueueName { get; set; }

    public ResumeRequest() { Type = MessageType.Resume.GetValue(); }

    public ResumeRequest(string requestId, string workflowId)
    {
        Type = MessageType.Resume.GetValue();
        RequestId = requestId;
        WorkflowId = workflowId;
    }
}
