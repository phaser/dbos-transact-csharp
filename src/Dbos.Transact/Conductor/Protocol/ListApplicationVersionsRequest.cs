namespace Dbos.Transact.Conductor.Protocol;

public sealed class ListApplicationVersionsRequest : BaseMessage
{
    public ListApplicationVersionsRequest() { Type = MessageType.ListApplicationVersions.GetValue(); }
}
