namespace Dbos.Transact.Workflow;

public enum ScheduleStatus
{
    Active = 0,
    Paused = 1,
}

public static class ScheduleStatusExtensions
{
    public static string ToDbString(this ScheduleStatus status) => status switch
    {
        ScheduleStatus.Active => "ACTIVE",
        ScheduleStatus.Paused => "PAUSED",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static ScheduleStatus ParseDbStatus(string status) => status switch
    {
        "ACTIVE" => ScheduleStatus.Active,
        "PAUSED" => ScheduleStatus.Paused,
        _ => throw new ArgumentException($"Unknown schedule status: '{status}'", nameof(status)),
    };
}
