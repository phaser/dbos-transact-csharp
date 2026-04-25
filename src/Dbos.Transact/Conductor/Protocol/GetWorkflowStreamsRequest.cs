using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class GetWorkflowStreamsRequest : BaseMessage
{
    [JsonPropertyName("workflow_id")]
    public string? WorkflowId { get; set; }

    public GetWorkflowStreamsRequest() { Type = MessageType.GetWorkflowStreams.GetValue(); }
}
