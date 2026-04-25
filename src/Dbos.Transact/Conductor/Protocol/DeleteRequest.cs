using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class DeleteRequest : BaseMessage
{
    [JsonPropertyName("workflow_id")]
    public string? WorkflowId { get; set; }

    [JsonPropertyName("workflow_ids")]
    public List<string>? WorkflowIds { get; set; }

    [JsonPropertyName("delete_children")]
    public bool DeleteChildren { get; set; }

    public DeleteRequest() { Type = MessageType.Delete.GetValue(); }

    public DeleteRequest(string requestId, string workflowId, bool deleteChildren)
    {
        Type = MessageType.Delete.GetValue();
        RequestId = requestId;
        WorkflowId = workflowId;
        DeleteChildren = deleteChildren;
    }
}
