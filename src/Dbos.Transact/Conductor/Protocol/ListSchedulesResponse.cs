using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class ListSchedulesResponse : BaseResponse
{
    [JsonPropertyName("output")]
    public List<ScheduleOutput> Output { get; set; } = [];

    public ListSchedulesResponse() { }

    public ListSchedulesResponse(BaseMessage message, List<ScheduleOutput> output)
        : base(message.Type, message.RequestId) => Output = output;

    public ListSchedulesResponse(BaseMessage message, string errorMessage)
        : base(message.Type, message.RequestId, errorMessage) { }
}
