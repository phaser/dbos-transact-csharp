using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class ExecutorInfoResponse : BaseResponse
{
    [JsonPropertyName("executor_id")]
    public string? ExecutorId { get; set; }

    [JsonPropertyName("application_version")]
    public string? ApplicationVersion { get; set; }

    [JsonPropertyName("hostname")]
    public string? Hostname { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("dbos_version")]
    public string? DbosVersion { get; set; }

    [JsonPropertyName("executor_metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? ExecutorMetadata { get; set; }

    public ExecutorInfoResponse() { }

    public ExecutorInfoResponse(
        BaseMessage message, string executorId, string appVersion, string hostname,
        string dbosVersion, Dictionary<string, object>? executorMetadata)
        : base(MessageType.ExecutorInfo.GetValue(), message.RequestId)
    {
        ExecutorId = executorId;
        ApplicationVersion = appVersion;
        Hostname = hostname;
        Language = "csharp";
        DbosVersion = dbosVersion;
        ExecutorMetadata = executorMetadata;
    }

    public ExecutorInfoResponse(BaseMessage message, Exception ex)
        : base(message.Type, message.RequestId, ex.Message) { }
}
