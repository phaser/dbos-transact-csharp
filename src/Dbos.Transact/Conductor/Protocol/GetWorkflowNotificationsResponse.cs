using System.Text.Json;
using System.Text.Json.Serialization;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class GetWorkflowNotificationsResponse : BaseResponse
{
    public sealed record NotificationOutput(
        [property: JsonPropertyName("topic")] string? Topic,
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("created_at_epoch_ms")] long? CreatedAtEpochMs,
        [property: JsonPropertyName("consumed")] bool Consumed)
    {
        public static NotificationOutput From(NotificationInfo info) => new(
            info.Topic,
            JsonSerializer.Serialize(info.Message),
            info.CreatedAtEpochMs,
            info.Consumed);
    }

    [JsonPropertyName("notifications")]
    public List<NotificationOutput> Notifications { get; set; } = [];

    public GetWorkflowNotificationsResponse() { }

    public GetWorkflowNotificationsResponse(BaseMessage message, List<NotificationInfo> infos)
        : base(message.Type, message.RequestId) =>
        Notifications = infos.ConvertAll(NotificationOutput.From);

    public GetWorkflowNotificationsResponse(BaseMessage message, Exception ex)
        : base(message.Type, message.RequestId, ex.Message) { }
}
