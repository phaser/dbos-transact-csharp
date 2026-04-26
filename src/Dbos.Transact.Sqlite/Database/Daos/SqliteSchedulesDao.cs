using System.Data.Common;
using Dbos.Transact.Database.Daos;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Sqlite.Database.Daos;

/// <summary>SQLite-backed implementation of <see cref="SchedulesDao"/>. Full SQL in DBOS-20.</summary>
public sealed class SqliteSchedulesDao : SchedulesDao
{
    private readonly Func<DbConnection> _connectionFactory;
    public SqliteSchedulesDao(Func<DbConnection> connectionFactory) { _connectionFactory = connectionFactory; }

    public override Task CreateScheduleAsync(WorkflowSchedule schedule, CancellationToken ct = default) => throw new NotImplementedException("DBOS-20");
    public override Task<IReadOnlyList<WorkflowSchedule>> ListSchedulesAsync(IReadOnlyList<ScheduleStatus>? statuses, IReadOnlyList<string>? workflowNames, IReadOnlyList<string>? scheduleNamePrefixes, CancellationToken ct = default) => throw new NotImplementedException("DBOS-20");
    public override Task<WorkflowSchedule?> GetScheduleAsync(string name, CancellationToken ct = default) => throw new NotImplementedException("DBOS-20");
    public override Task PauseScheduleAsync(string name, CancellationToken ct = default) => throw new NotImplementedException("DBOS-20");
    public override Task ResumeScheduleAsync(string name, CancellationToken ct = default) => throw new NotImplementedException("DBOS-20");
    public override Task UpdateScheduleLastFiredAtAsync(string name, DateTimeOffset lastFiredAt, CancellationToken ct = default) => throw new NotImplementedException("DBOS-20");
    public override Task DeleteScheduleAsync(string name, CancellationToken ct = default) => throw new NotImplementedException("DBOS-20");
    public override Task ApplySchedulesAsync(IReadOnlyList<WorkflowSchedule> schedules, CancellationToken ct = default) => throw new NotImplementedException("DBOS-20");
}
