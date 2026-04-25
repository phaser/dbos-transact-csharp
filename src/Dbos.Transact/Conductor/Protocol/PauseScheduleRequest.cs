using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class PauseScheduleRequest : BaseMessage
{
    [JsonPropertyName("schedule_name")]
    public string? ScheduleName { get; set; }

    public PauseScheduleRequest() { Type = MessageType.PauseSchedule.GetValue(); }

    public PauseScheduleRequest(string requestId, string scheduleName)
    {
        Type = MessageType.PauseSchedule.GetValue();
        RequestId = requestId;
        ScheduleName = scheduleName;
    }
}
