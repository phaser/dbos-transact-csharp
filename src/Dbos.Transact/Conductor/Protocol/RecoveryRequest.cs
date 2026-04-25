using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class RecoveryRequest : BaseMessage
{
    [JsonPropertyName("executor_ids")]
    public List<string>? ExecutorIds { get; set; }

    public RecoveryRequest() { Type = MessageType.Recovery.GetValue(); }

    public RecoveryRequest(string requestId, List<string> executorIds)
    {
        Type = MessageType.Recovery.GetValue();
        RequestId = requestId;
        ExecutorIds = executorIds;
    }
}
