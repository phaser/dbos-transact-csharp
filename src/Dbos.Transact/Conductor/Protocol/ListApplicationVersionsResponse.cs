using System.Text.Json.Serialization;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class ListApplicationVersionsResponse : BaseResponse
{
    public sealed record AppVersionInfo(
        [property: JsonPropertyName("version_id")] string VersionId,
        [property: JsonPropertyName("version_name")] string VersionName,
        [property: JsonPropertyName("version_timestamp")] long VersionTimestamp,
        [property: JsonPropertyName("created_at")] long CreatedAt)
    {
        public static AppVersionInfo FromVersionInfo(VersionInfo v) => new(
            v.VersionId, v.VersionName,
            v.VersionTimestamp.ToUnixTimeMilliseconds(),
            v.CreatedAt.ToUnixTimeMilliseconds());
    }

    [JsonPropertyName("output")]
    public List<AppVersionInfo> Output { get; set; } = [];

    public ListApplicationVersionsResponse() { }

    public ListApplicationVersionsResponse(BaseMessage message, List<VersionInfo> versions)
        : base(message.Type, message.RequestId) =>
        Output = versions.ConvertAll(AppVersionInfo.FromVersionInfo);

    public ListApplicationVersionsResponse(BaseMessage message, Exception ex)
        : base(message.Type, message.RequestId, ex.Message) { }
}
