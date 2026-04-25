using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class GetWorkflowEventsResponse : BaseResponse
{
    public sealed record EventOutput(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("value")] string Value)
    {
        public static EventOutput From(KeyValuePair<string, object?> kv) =>
            new(kv.Key, JsonSerializer.Serialize(kv.Value));
    }

    [JsonPropertyName("events")]
    public List<EventOutput> Events { get; set; } = [];

    public GetWorkflowEventsResponse() { }

    public GetWorkflowEventsResponse(BaseMessage message, Dictionary<string, object?> events)
        : base(message.Type, message.RequestId) =>
        Events = [..events.Select(EventOutput.From)];

    public GetWorkflowEventsResponse(BaseMessage message, Exception ex)
        : base(message.Type, message.RequestId, ex.Message) { }
}
