using System.Data.Common;
using Dapper;
using Dbos.Transact.Database.Daos;
using Dbos.Transact.Json;
using Dbos.Transact.Workflow;
using Npgsql;

namespace Dbos.Transact.Postgres.Database.Daos;

/// <summary>PostgreSQL-backed implementation of <see cref="SchedulesDao"/>. Port of Java <c>SchedulesDAO</c>.</summary>
public sealed class PostgresSchedulesDao : SchedulesDao
{
    private readonly Func<DbConnection> _connectionFactory;
    private readonly string _schemaPrefix;
    private readonly IDbosSerializer _serializer;

    public PostgresSchedulesDao(Func<DbConnection> connectionFactory, string schema, IDbosSerializer serializer)
    {
        _connectionFactory = connectionFactory;
        _schemaPrefix = string.IsNullOrEmpty(schema) ? string.Empty : $"\"{schema}\".";
        _serializer = serializer;
    }

    private sealed class ScheduleRow
    {
        public string? ScheduleId { get; set; }
        public string? ScheduleName { get; set; }
        public string? WorkflowName { get; set; }
        public string? WorkflowClassName { get; set; }
        public string? Schedule { get; set; }
        public string? Status { get; set; }
        public string? Context { get; set; }
        public string? LastFiredAt { get; set; }
        public bool AutomaticBackfill { get; set; }
        public string? CronTimezone { get; set; }
        public string? QueueName { get; set; }
    }

    private const string ScheduleColumns = """
        schedule_id AS ScheduleId,
        schedule_name AS ScheduleName,
        workflow_name AS WorkflowName,
        workflow_class_name AS WorkflowClassName,
        schedule AS Schedule,
        status AS Status,
        context AS Context,
        last_fired_at AS LastFiredAt,
        automatic_backfill AS AutomaticBackfill,
        cron_timezone AS CronTimezone,
        queue_name AS QueueName
        """;

    // The migrated `context` column is NOT NULL, so a null context is stored as the
    // JSON literal "null" (matches what Jackson would emit). On read, we decode the
    // sentinel back to null without invoking the serializer envelope check.
    private const string NullContextSentinel = "null";

    private WorkflowSchedule RowToSchedule(ScheduleRow row)
    {
        var context = row.Context == NullContextSentinel ? null : _serializer.Deserialize(row.Context);
        var lastFiredAt = row.LastFiredAt is null
            ? (DateTimeOffset?)null
            : DateTimeOffset.Parse(row.LastFiredAt, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
        var tz = row.CronTimezone is null ? null : TimeZoneInfo.FindSystemTimeZoneById(row.CronTimezone);

        return new WorkflowSchedule(
            Id: row.ScheduleId,
            ScheduleName: row.ScheduleName!,
            WorkflowName: row.WorkflowName!,
            ClassName: row.WorkflowClassName,
            Cron: row.Schedule!,
            Status: ScheduleStatusExtensions.ParseDbStatus(row.Status!),
            Context: context,
            LastFiredAt: lastFiredAt,
            AutomaticBackfill: row.AutomaticBackfill,
            CronTimezone: tz,
            QueueName: row.QueueName);
    }

    public override async Task CreateScheduleAsync(WorkflowSchedule schedule, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await CreateScheduleAsync(connection, transaction: null, schedule, ct).ConfigureAwait(false);
    }

    private async Task CreateScheduleAsync(DbConnection connection, DbTransaction? transaction, WorkflowSchedule schedule, CancellationToken ct)
    {
        var sql = $"""
            INSERT INTO {_schemaPrefix}workflow_schedules
                (schedule_id, schedule_name, workflow_name, workflow_class_name,
                 schedule, status, context, last_fired_at, automatic_backfill,
                 cron_timezone, queue_name)
            VALUES (@ScheduleId, @ScheduleName, @WorkflowName, @WorkflowClassName,
                    @Schedule, @Status, @Context, @LastFiredAt, @AutomaticBackfill,
                    @CronTimezone, @QueueName)
            """;

        var p = new
        {
            ScheduleId = schedule.Id ?? Guid.NewGuid().ToString(),
            ScheduleName = schedule.ScheduleName,
            WorkflowName = schedule.WorkflowName,
            WorkflowClassName = schedule.ClassName,
            Schedule = schedule.Cron,
            Status = schedule.Status.ToDbString(),
            Context = _serializer.Serialize(schedule.Context) ?? NullContextSentinel,
            LastFiredAt = schedule.LastFiredAt?.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
            AutomaticBackfill = schedule.AutomaticBackfill,
            CronTimezone = schedule.CronTimezone?.Id,
            QueueName = schedule.QueueName,
        };

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(sql, p, transaction: transaction, cancellationToken: ct)).ConfigureAwait(false);
        }
        catch (NpgsqlException ex) when (ex.SqlState == "23505")
        {
            throw new InvalidOperationException($"Schedule '{schedule.ScheduleName}' already exists", ex);
        }
    }

    public override async Task<IReadOnlyList<WorkflowSchedule>> ListSchedulesAsync(
        IReadOnlyList<ScheduleStatus>? statuses,
        IReadOnlyList<string>? workflowNames,
        IReadOnlyList<string>? scheduleNamePrefixes,
        CancellationToken ct = default)
    {
        var sql = new System.Text.StringBuilder($"SELECT {ScheduleColumns} FROM {_schemaPrefix}workflow_schedules WHERE TRUE");
        var parameters = new DynamicParameters();

        if (statuses is { Count: > 0 })
        {
            sql.Append(" AND status = ANY(@Statuses)");
            parameters.Add("Statuses", statuses.Select(s => s.ToDbString()).ToArray());
        }
        if (workflowNames is { Count: > 0 })
        {
            sql.Append(" AND workflow_name = ANY(@WorkflowNames)");
            parameters.Add("WorkflowNames", workflowNames.ToArray());
        }
        if (scheduleNamePrefixes is { Count: > 0 })
        {
            sql.Append(" AND (");
            for (int i = 0; i < scheduleNamePrefixes.Count; i++)
            {
                if (i > 0) sql.Append(" OR ");
                sql.Append("schedule_name LIKE @Prefix").Append(i);
                var escaped = scheduleNamePrefixes[i].Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
                parameters.Add(string.Concat("Prefix", i.ToString(System.Globalization.CultureInfo.InvariantCulture)), escaped);
            }
            sql.Append(')');
        }

        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ScheduleRow>(
            new CommandDefinition(sql.ToString(), parameters, cancellationToken: ct)).ConfigureAwait(false);
        return rows.Select(RowToSchedule).ToList();
    }

    public override async Task<WorkflowSchedule?> GetScheduleAsync(string name, CancellationToken ct = default)
    {
        var sql = $"""
            SELECT {ScheduleColumns}
            FROM {_schemaPrefix}workflow_schedules
            WHERE schedule_name = @Name
            """;
        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<ScheduleRow>(
            new CommandDefinition(sql, new { Name = name }, cancellationToken: ct)).ConfigureAwait(false);
        return row is null ? null : RowToSchedule(row);
    }

    public override Task PauseScheduleAsync(string name, CancellationToken ct = default) =>
        SetScheduleStatusAsync(name, ScheduleStatus.Paused, ct);

    public override Task ResumeScheduleAsync(string name, CancellationToken ct = default) =>
        SetScheduleStatusAsync(name, ScheduleStatus.Active, ct);

    private async Task SetScheduleStatusAsync(string name, ScheduleStatus status, CancellationToken ct)
    {
        var sql = $"UPDATE {_schemaPrefix}workflow_schedules SET status = @Status WHERE schedule_name = @Name";
        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Status = status.ToDbString(), Name = name }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public override async Task UpdateScheduleLastFiredAtAsync(string name, DateTimeOffset lastFiredAt, CancellationToken ct = default)
    {
        var sql = $"UPDATE {_schemaPrefix}workflow_schedules SET last_fired_at = @LastFiredAt WHERE schedule_name = @Name";
        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(sql,
            new { LastFiredAt = lastFiredAt.ToString("o", System.Globalization.CultureInfo.InvariantCulture), Name = name },
            cancellationToken: ct)).ConfigureAwait(false);
    }

    public override async Task DeleteScheduleAsync(string name, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await DeleteScheduleAsync(connection, transaction: null, name, ct).ConfigureAwait(false);
    }

    private async Task DeleteScheduleAsync(DbConnection connection, DbTransaction? transaction, string name, CancellationToken ct)
    {
        var sql = $"DELETE FROM {_schemaPrefix}workflow_schedules WHERE schedule_name = @Name";
        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Name = name }, transaction: transaction, cancellationToken: ct)).ConfigureAwait(false);
    }

    public override async Task ApplySchedulesAsync(IReadOnlyList<WorkflowSchedule> schedules, CancellationToken ct = default)
    {
        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            foreach (var schedule in schedules)
            {
                await DeleteScheduleAsync(connection, tx, schedule.ScheduleName, ct).ConfigureAwait(false);
                var fresh = schedule with
                {
                    Id = Guid.NewGuid().ToString(),
                    Status = ScheduleStatus.Active,
                    LastFiredAt = null,
                };
                await CreateScheduleAsync(connection, tx, fresh, ct).ConfigureAwait(false);
            }
            await tx.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            try { await tx.RollbackAsync(ct).ConfigureAwait(false); } catch { /* ignore */ }
            throw;
        }
    }
}
