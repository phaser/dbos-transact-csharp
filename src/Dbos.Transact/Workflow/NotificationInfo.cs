namespace Dbos.Transact.Workflow;

public sealed record NotificationInfo(
    string? Topic,
    object? Message,
    DateTimeOffset? CreatedAt,
    bool Consumed)
{
    public long? CreatedAtEpochMs => CreatedAt?.ToUnixTimeMilliseconds();
}
