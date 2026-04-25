using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class TriggerScheduleResponse : BaseResponse
{
    [JsonPropertyName("workflow_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WorkflowId { get; set; }

    public TriggerScheduleResponse() { }

    public TriggerScheduleResponse(BaseMessage message, string? workflowId)
        : base(message.Type, message.RequestId) => WorkflowId = workflowId;

    public TriggerScheduleResponse(BaseMessage message, string errorMessage, bool _)
        : base(message.Type, message.RequestId, errorMessage) { }
}
