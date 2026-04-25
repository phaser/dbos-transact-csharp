using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class GetWorkflowNotificationsRequest : BaseMessage
{
    [JsonPropertyName("workflow_id")]
    public string? WorkflowId { get; set; }

    public GetWorkflowNotificationsRequest() { Type = MessageType.GetWorkflowNotifications.GetValue(); }
}
