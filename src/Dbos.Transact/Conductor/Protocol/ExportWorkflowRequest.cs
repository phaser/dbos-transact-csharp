using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class ExportWorkflowRequest : BaseMessage
{
    [JsonPropertyName("workflow_id")]
    public string? WorkflowId { get; set; }

    [JsonPropertyName("export_children")]
    public bool ExportChildren { get; set; }

    public ExportWorkflowRequest() { Type = MessageType.ExportWorkflow.GetValue(); }

    public ExportWorkflowRequest(string requestId, string workflowId, bool exportChildren)
    {
        Type = MessageType.ExportWorkflow.GetValue();
        RequestId = requestId;
        WorkflowId = workflowId;
        ExportChildren = exportChildren;
    }
}
