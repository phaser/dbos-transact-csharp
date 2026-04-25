using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class TriggerScheduleRequest : BaseMessage
{
    [JsonPropertyName("schedule_name")]
    public string? ScheduleName { get; set; }

    public TriggerScheduleRequest() { Type = MessageType.TriggerSchedule.GetValue(); }

    public TriggerScheduleRequest(string requestId, string scheduleName)
    {
        Type = MessageType.TriggerSchedule.GetValue();
        RequestId = requestId;
        ScheduleName = scheduleName;
    }
}
