using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class RestartRequest : BaseMessage
{
    [JsonPropertyName("workflow_id")]
    public string? WorkflowId { get; set; }

    public RestartRequest() { Type = MessageType.Restart.GetValue(); }

    public RestartRequest(string requestId, string workflowId)
    {
        Type = MessageType.Restart.GetValue();
        RequestId = requestId;
        WorkflowId = workflowId;
    }
}
