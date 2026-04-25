namespace Dbos.Transact.Conductor.Protocol;

public sealed class ExecutorInfoRequest : BaseMessage
{
    public ExecutorInfoRequest() { Type = MessageType.ExecutorInfo.GetValue(); }

    public ExecutorInfoRequest(string requestId)
    {
        Type = MessageType.ExecutorInfo.GetValue();
        RequestId = requestId;
    }
}
