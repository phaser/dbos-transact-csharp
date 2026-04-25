using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class AlertRequest : BaseMessage
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    public AlertRequest() { Type = MessageType.Alert.GetValue(); }

    public AlertRequest(string requestId, string name, string message, Dictionary<string, string>? metadata)
    {
        Type = MessageType.Alert.GetValue();
        RequestId = requestId;
        Name = name;
        Message = message;
        Metadata = metadata;
    }
}
