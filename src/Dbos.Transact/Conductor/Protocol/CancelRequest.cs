using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class CancelRequest : BaseMessage
{
    [JsonPropertyName("workflow_id")]
    public string? WorkflowId { get; set; }

    [JsonPropertyName("workflow_ids")]
    public List<string>? WorkflowIds { get; set; }

    public CancelRequest() { Type = MessageType.Cancel.GetValue(); }

    public CancelRequest(string requestId, string workflowId)
    {
        Type = MessageType.Cancel.GetValue();
        RequestId = requestId;
        WorkflowId = workflowId;
    }
}
