using Dbos.Transact.Workflow;

namespace Dbos.Transact.Database.Daos;

/// <summary>
/// Data-access methods for the <c>workflow_schedules</c> table.
/// Port of Java's <c>SchedulesDAO</c>. Dialect-specific SQL is provided by subclasses.
/// </summary>
public abstract class SchedulesDao
{
    public abstract Task CreateScheduleAsync(WorkflowSchedule schedule, CancellationToken ct = default);

    public abstract Task<IReadOnlyList<WorkflowSchedule>> ListSchedulesAsync(
        IReadOnlyList<ScheduleStatus>? statuses,
        IReadOnlyList<string>? workflowNames,
        IReadOnlyList<string>? scheduleNamePrefixes,
        CancellationToken ct = default);

    public abstract Task<WorkflowSchedule?> GetScheduleAsync(string name, CancellationToken ct = default);

    public abstract Task PauseScheduleAsync(string name, CancellationToken ct = default);

    public abstract Task ResumeScheduleAsync(string name, CancellationToken ct = default);

    public abstract Task UpdateScheduleLastFiredAtAsync(string name, DateTimeOffset lastFiredAt, CancellationToken ct = default);

    public abstract Task DeleteScheduleAsync(string name, CancellationToken ct = default);

    public abstract Task ApplySchedulesAsync(IReadOnlyList<WorkflowSchedule> schedules, CancellationToken ct = default);
}
