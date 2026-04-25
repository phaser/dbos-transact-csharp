using System.Text.Json.Serialization;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class ListSchedulesRequest : BaseMessage
{
    [JsonPropertyName("body")]
    public SchedulesBody? Body { get; set; }

    public ListSchedulesRequest() { Type = MessageType.ListSchedules.GetValue(); }

    public ListSchedulesRequest(string requestId, SchedulesBody body)
    {
        Type = MessageType.ListSchedules.GetValue();
        RequestId = requestId;
        Body = body;
    }

    public sealed record SchedulesBody(
        [property: JsonConverter(typeof(StringOrListConverter))]
        [property: JsonPropertyName("status")] List<string>? Status,
        [property: JsonConverter(typeof(StringOrListConverter))]
        [property: JsonPropertyName("workflow_name")] List<string>? WorkflowName,
        [property: JsonConverter(typeof(StringOrListConverter))]
        [property: JsonPropertyName("schedule_name_prefix")] List<string>? ScheduleNamePrefix,
        [property: JsonPropertyName("load_context")] bool? LoadContext);

    public List<ScheduleStatus>? ParsedStatuses() =>
        Body?.Status?.ConvertAll(Enum.Parse<ScheduleStatus>);

    public List<string>? WorkflowNames() => Body?.WorkflowName;
    public List<string>? ScheduleNamePrefixes() => Body?.ScheduleNamePrefix;
    public bool ShouldLoadContext() => Body?.LoadContext is true;
}
