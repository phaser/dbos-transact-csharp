using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class GetScheduleResponse : BaseResponse
{
    [JsonPropertyName("output")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScheduleOutput? Output { get; set; }

    public GetScheduleResponse() { }

    public GetScheduleResponse(BaseMessage message, ScheduleOutput? output)
        : base(message.Type, message.RequestId) => Output = output;

    public GetScheduleResponse(BaseMessage message, string errorMessage)
        : base(message.Type, message.RequestId, errorMessage) { }
}
