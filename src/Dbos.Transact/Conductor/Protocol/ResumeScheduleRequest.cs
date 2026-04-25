using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class ResumeScheduleRequest : BaseMessage
{
    [JsonPropertyName("schedule_name")]
    public string? ScheduleName { get; set; }

    public ResumeScheduleRequest() { Type = MessageType.ResumeSchedule.GetValue(); }

    public ResumeScheduleRequest(string requestId, string scheduleName)
    {
        Type = MessageType.ResumeSchedule.GetValue();
        RequestId = requestId;
        ScheduleName = scheduleName;
    }
}
