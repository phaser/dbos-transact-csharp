using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class GetScheduleRequest : BaseMessage
{
    [JsonPropertyName("schedule_name")]
    public string? ScheduleName { get; set; }

    [JsonPropertyName("load_context")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? LoadContext { get; set; }

    public GetScheduleRequest() { Type = MessageType.GetSchedule.GetValue(); }

    public GetScheduleRequest(string requestId, string scheduleName, bool? loadContext)
    {
        Type = MessageType.GetSchedule.GetValue();
        RequestId = requestId;
        ScheduleName = scheduleName;
        LoadContext = loadContext;
    }

    public bool ShouldLoadContext => LoadContext is true;
}
