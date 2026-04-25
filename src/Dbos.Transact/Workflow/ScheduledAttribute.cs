namespace Dbos.Transact.Workflow;

/// <summary>
/// Marks a workflow method as a scheduled (cron) entry point.
/// Port of Java <c>@Scheduled</c> annotation.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ScheduledAttribute : Attribute
{
    public ScheduledAttribute(string cron) => Cron = cron;

    /// <summary>Cron expression defining the schedule.</summary>
    public string Cron { get; }

    /// <summary>Optional queue name to use when dispatching scheduled runs.</summary>
    public string Queue { get; set; } = "";

    /// <summary>Whether missed executions should be automatically backfilled.</summary>
    public bool AutomaticBackfill { get; set; }
}
