using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class SetLatestApplicationVersionRequest : BaseMessage
{
    [JsonPropertyName("version_name")]
    public string? VersionName { get; set; }

    public SetLatestApplicationVersionRequest() { Type = MessageType.SetLatestApplicationVersion.GetValue(); }
}
