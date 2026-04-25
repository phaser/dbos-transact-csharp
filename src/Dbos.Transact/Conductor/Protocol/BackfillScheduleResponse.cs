using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class BackfillScheduleResponse : BaseResponse
{
    [JsonPropertyName("workflow_ids")]
    public List<string> WorkflowIds { get; set; } = [];

    public BackfillScheduleResponse() { }

    public BackfillScheduleResponse(BaseMessage message, List<string> workflowIds)
        : base(message.Type, message.RequestId) => WorkflowIds = workflowIds;

    public BackfillScheduleResponse(BaseMessage message, string errorMessage)
        : base(message.Type, message.RequestId, errorMessage) { }
}
