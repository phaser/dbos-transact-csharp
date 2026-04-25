using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class ExistPendingWorkflowsResponse : BaseResponse
{
    [JsonPropertyName("exist")]
    public bool Exist { get; set; }

    public ExistPendingWorkflowsResponse() { }

    public ExistPendingWorkflowsResponse(BaseMessage message, bool exist)
        : base(message.Type, message.RequestId) => Exist = exist;

    public ExistPendingWorkflowsResponse(BaseMessage message, Exception ex)
        : base(message.Type, message.RequestId, ex.Message) { }
}
