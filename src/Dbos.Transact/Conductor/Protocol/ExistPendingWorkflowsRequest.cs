using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class ExistPendingWorkflowsRequest : BaseMessage
{
    [JsonPropertyName("executor_id")]
    public string? ExecutorId { get; set; }

    [JsonPropertyName("application_version")]
    public string? ApplicationVersion { get; set; }

    public ExistPendingWorkflowsRequest() { Type = MessageType.ExistPendingWorkflows.GetValue(); }

    public ExistPendingWorkflowsRequest(string requestId, string executorId, string appVer)
    {
        Type = MessageType.ExistPendingWorkflows.GetValue();
        RequestId = requestId;
        ExecutorId = executorId;
        ApplicationVersion = appVer;
    }
}
