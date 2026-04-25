using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class ForkWorkflowResponse : BaseResponse
{
    [JsonPropertyName("new_workflow_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NewWorkflowId { get; set; }

    public ForkWorkflowResponse() { }

    public ForkWorkflowResponse(BaseMessage message, string? newWorkflowId, string? errorMessage = null)
        : base(message.Type, message.RequestId, errorMessage) => NewWorkflowId = newWorkflowId;

    public ForkWorkflowResponse(BaseMessage message, Exception ex)
        : base(message.Type, message.RequestId, ex.Message) { }
}
