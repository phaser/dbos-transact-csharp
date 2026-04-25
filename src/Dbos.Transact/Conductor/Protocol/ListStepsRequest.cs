using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class ListStepsRequest : BaseMessage
{
    [JsonPropertyName("workflow_id")]
    public string? WorkflowId { get; set; }

    [JsonPropertyName("load_output")]
    public bool LoadOutput { get; set; } = true;

    [JsonPropertyName("limit")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Limit { get; set; }

    [JsonPropertyName("offset")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Offset { get; set; }

    public ListStepsRequest() { Type = MessageType.ListSteps.GetValue(); }

    public ListStepsRequest(string requestId, string workflowId)
    {
        Type = MessageType.ListSteps.GetValue();
        RequestId = requestId;
        WorkflowId = workflowId;
    }
}
