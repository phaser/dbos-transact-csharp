using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class GetWorkflowEventsRequest : BaseMessage
{
    [JsonPropertyName("workflow_id")]
    public string? WorkflowId { get; set; }

    public GetWorkflowEventsRequest() { Type = MessageType.GetWorkflowEvents.GetValue(); }
}
