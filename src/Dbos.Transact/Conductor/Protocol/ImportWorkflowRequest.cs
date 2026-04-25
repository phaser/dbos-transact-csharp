using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class ImportWorkflowRequest : BaseMessage
{
    [JsonPropertyName("serialized_workflow")]
    public string? SerializedWorkflow { get; set; }

    public ImportWorkflowRequest() { Type = MessageType.ImportWorkflow.GetValue(); }

    public ImportWorkflowRequest(string requestId, string serializedWorkflow)
    {
        Type = MessageType.ImportWorkflow.GetValue();
        RequestId = requestId;
        SerializedWorkflow = serializedWorkflow;
    }
}
