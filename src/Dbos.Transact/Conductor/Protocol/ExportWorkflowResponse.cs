using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class ExportWorkflowResponse : BaseResponse
{
    [JsonPropertyName("serialized_workflow")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SerializedWorkflow { get; set; }

    public ExportWorkflowResponse() { }

    public ExportWorkflowResponse(BaseMessage message, string? serializedWorkflow)
        : base(MessageType.ExportWorkflow.GetValue(), message.RequestId) =>
        SerializedWorkflow = serializedWorkflow;

    public ExportWorkflowResponse(BaseMessage message, Exception ex)
        : base(message.Type, message.RequestId, ex.Message) { }
}
