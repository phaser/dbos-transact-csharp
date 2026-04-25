using System.Text.Json.Serialization;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class GetWorkflowRequest : BaseMessage
{
    [JsonPropertyName("workflow_id")]
    public string? WorkflowId { get; set; }

    [JsonPropertyName("load_input")]
    public bool LoadInput { get; set; } = true;

    [JsonPropertyName("load_output")]
    public bool LoadOutput { get; set; } = true;

    public GetWorkflowRequest() { Type = MessageType.GetWorkflow.GetValue(); }

    public GetWorkflowRequest(string requestId, string workflowId)
    {
        Type = MessageType.GetWorkflow.GetValue();
        RequestId = requestId;
        WorkflowId = workflowId;
    }

    public ListWorkflowsInput ToInput() => new(
        WorkflowId is null ? null : [WorkflowId],
        null, null, null, null, null, null, null, null, null, null, null, null,
        LoadInput, LoadOutput,
        null, null, null, null, null, null, null);
}
