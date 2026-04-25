namespace Dbos.Transact.Workflow;

public sealed record WorkflowSchedule(
    string? Id,
    string ScheduleName,
    string WorkflowName,
    string? ClassName,
    string Cron,
    ScheduleStatus Status,
    object? Context,
    DateTimeOffset? LastFiredAt,
    bool AutomaticBackfill,
    TimeZoneInfo? CronTimezone,
    string? QueueName)
{
    public string ScheduleName { get; init; } = string.IsNullOrEmpty(ScheduleName)
        ? throw new ArgumentException("ScheduleName must not be null or empty.", nameof(ScheduleName))
        : ScheduleName;

    public string WorkflowName { get; init; } = string.IsNullOrEmpty(WorkflowName)
        ? throw new ArgumentException("WorkflowName must not be null or empty.", nameof(WorkflowName))
        : WorkflowName;

    public string Cron { get; init; } = string.IsNullOrEmpty(Cron)
        ? throw new ArgumentException("Cron must not be null or empty.", nameof(Cron))
        : Cron;

    public WorkflowSchedule(
        string scheduleName,
        string workflowName,
        string? className,
        string cron)
        : this(null, scheduleName, workflowName, className, cron,
               ScheduleStatus.Active, null, null, false, null, null)
    {
    }

    public bool IsActive => Status == ScheduleStatus.Active;
}
