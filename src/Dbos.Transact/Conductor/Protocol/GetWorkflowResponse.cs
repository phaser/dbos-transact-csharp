using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class GetWorkflowResponse : BaseResponse
{
    [JsonPropertyName("output")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkflowsOutput? Output { get; set; }

    public GetWorkflowResponse() { }

    public GetWorkflowResponse(BaseMessage message, WorkflowsOutput? output)
        : base(message.Type, message.RequestId) => Output = output;

    public GetWorkflowResponse(BaseMessage message, Exception ex)
        : base(message.Type, message.RequestId, ex.Message) { }
}
