using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class BackfillScheduleRequest : BaseMessage
{
    [JsonPropertyName("schedule_name")]
    public string? ScheduleName { get; set; }

    [JsonPropertyName("start")]
    public string? Start { get; set; }

    [JsonPropertyName("end")]
    public string? End { get; set; }

    public BackfillScheduleRequest() { Type = MessageType.BackfillSchedule.GetValue(); }

    public BackfillScheduleRequest(string requestId, string scheduleName, string? start, string? end)
    {
        Type = MessageType.BackfillSchedule.GetValue();
        RequestId = requestId;
        ScheduleName = scheduleName;
        Start = start;
        End = end;
    }
}
