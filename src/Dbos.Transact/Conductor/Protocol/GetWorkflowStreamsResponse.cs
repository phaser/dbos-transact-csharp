using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class GetWorkflowStreamsResponse : BaseResponse
{
    public sealed record StreamEntryOutput(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("values")] List<string> Values)
    {
        public static StreamEntryOutput From(string key, List<object?> values) =>
            new(key, values.ConvertAll(v => JsonSerializer.Serialize(v)));
    }

    [JsonPropertyName("streams")]
    public List<StreamEntryOutput> Streams { get; set; } = [];

    public GetWorkflowStreamsResponse() { }

    public GetWorkflowStreamsResponse(BaseMessage message, Dictionary<string, List<object?>> streamData)
        : base(message.Type, message.RequestId) =>
        Streams = [..streamData.Select(e => StreamEntryOutput.From(e.Key, e.Value))];

    public GetWorkflowStreamsResponse(BaseMessage message, Exception ex)
        : base(message.Type, message.RequestId, ex.Message) { }
}
