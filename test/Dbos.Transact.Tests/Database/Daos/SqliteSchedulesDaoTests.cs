using Dbos.Transact.Database;
using Dbos.Transact.Migrations;
using Dbos.Transact.Sqlite.Database;
using Dbos.Transact.Tests.Fixtures;
using Dbos.Transact.Workflow;
using Microsoft.Data.Sqlite;

namespace Dbos.Transact.Tests.Database.Daos;

public sealed class SqliteSchedulesDaoTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteFixture _fixture = new(SqliteFixture.Mode.File);

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();
    public void Dispose() { _fixture.Dispose(); GC.SuppressFinalize(this); }

    private async Task<SystemDatabase> CreateAsync()
    {
        await using var conn = new SqliteConnection(_fixture.ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Sqlite);
        await mgr.RunAsync();
        return new SqliteSystemDatabase(_fixture.ConnectionString);
    }

    private static WorkflowSchedule MakeSchedule(string name, ScheduleStatus status = ScheduleStatus.Active) =>
        new WorkflowSchedule(scheduleName: name, workflowName: "MyApp.Workflow", className: "MyApp", cron: "0 0 * * * *") with { Status = status };

    [Fact]
    public async Task CreateAndGet_RoundTripsAllFields()
    {
        await using var db = await CreateAsync();

        var schedule = new WorkflowSchedule(
            Id: "sched-id-1",
            ScheduleName: "every-hour",
            WorkflowName: "MyApp.Workflow",
            ClassName: "MyApp",
            Cron: "0 0 * * * *",
            Status: ScheduleStatus.Paused,
            Context: new Dictionary<string, object?> { ["foo"] = "bar" },
            LastFiredAt: new DateTimeOffset(2026, 4, 27, 10, 0, 0, TimeSpan.Zero),
            AutomaticBackfill: true,
            CronTimezone: TimeZoneInfo.Utc,
            QueueName: "queue-x");

        await db.CreateScheduleAsync(schedule);
        var fetched = await db.GetScheduleAsync("every-hour");

        Assert.NotNull(fetched);
        Assert.Equal("sched-id-1", fetched!.Id);
        Assert.Equal("every-hour", fetched.ScheduleName);
        Assert.Equal("MyApp.Workflow", fetched.WorkflowName);
        Assert.Equal("MyApp", fetched.ClassName);
        Assert.Equal("0 0 * * * *", fetched.Cron);
        Assert.Equal(ScheduleStatus.Paused, fetched.Status);
        Assert.Equal(schedule.LastFiredAt, fetched.LastFiredAt);
        Assert.True(fetched.AutomaticBackfill);
        Assert.Equal(TimeZoneInfo.Utc.Id, fetched.CronTimezone?.Id);
        Assert.Equal("queue-x", fetched.QueueName);
    }

    [Fact]
    public async Task Create_NullId_AssignsGeneratedGuid()
    {
        await using var db = await CreateAsync();
        await db.CreateScheduleAsync(MakeSchedule("auto-id"));
        var fetched = await db.GetScheduleAsync("auto-id");
        Assert.NotNull(fetched!.Id);
        Assert.True(Guid.TryParse(fetched.Id, out _));
    }

    [Fact]
    public async Task Create_DuplicateName_Throws()
    {
        await using var db = await CreateAsync();
        await db.CreateScheduleAsync(MakeSchedule("dup"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.CreateScheduleAsync(MakeSchedule("dup")));
    }

    [Fact]
    public async Task GetSchedule_Missing_ReturnsNull()
    {
        await using var db = await CreateAsync();
        Assert.Null(await db.GetScheduleAsync("nope"));
    }

    [Fact]
    public async Task ListSchedules_Empty_ReturnsEmpty()
    {
        await using var db = await CreateAsync();
        Assert.Empty(await db.ListSchedulesAsync());
    }

    [Fact]
    public async Task ListSchedules_FilterByStatus()
    {
        await using var db = await CreateAsync();
        await db.CreateScheduleAsync(MakeSchedule("a"));
        await db.CreateScheduleAsync(MakeSchedule("b", ScheduleStatus.Paused));
        await db.CreateScheduleAsync(MakeSchedule("c"));

        var paused = await db.ListSchedulesAsync(statuses: [ScheduleStatus.Paused]);
        Assert.Single(paused);
        Assert.Equal("b", paused[0].ScheduleName);
    }

    [Fact]
    public async Task ListSchedules_FilterByPrefix_EscapesWildcards()
    {
        await using var db = await CreateAsync();
        await db.CreateScheduleAsync(MakeSchedule("foo-1"));
        await db.CreateScheduleAsync(MakeSchedule("foo-2"));
        await db.CreateScheduleAsync(MakeSchedule("bar-1"));
        await db.CreateScheduleAsync(MakeSchedule("foo%special"));

        var foo = await db.ListSchedulesAsync(scheduleNamePrefixes: ["foo-"]);
        Assert.Equal(2, foo.Count);
        Assert.All(foo, s => Assert.StartsWith("foo-", s.ScheduleName));

        var literal = await db.ListSchedulesAsync(scheduleNamePrefixes: ["foo%"]);
        Assert.Single(literal);
        Assert.Equal("foo%special", literal[0].ScheduleName);
    }

    [Fact]
    public async Task PauseAndResume_ChangesStatus()
    {
        await using var db = await CreateAsync();
        await db.CreateScheduleAsync(MakeSchedule("toggle"));
        await db.PauseScheduleAsync("toggle");
        Assert.Equal(ScheduleStatus.Paused, (await db.GetScheduleAsync("toggle"))!.Status);
        await db.ResumeScheduleAsync("toggle");
        Assert.Equal(ScheduleStatus.Active, (await db.GetScheduleAsync("toggle"))!.Status);
    }

    [Fact]
    public async Task UpdateLastFiredAt_PersistsRoundTrip()
    {
        await using var db = await CreateAsync();
        await db.CreateScheduleAsync(MakeSchedule("ts"));
        var when = new DateTimeOffset(2026, 1, 1, 12, 30, 45, TimeSpan.Zero);
        await db.UpdateScheduleLastFiredAtAsync("ts", when);
        var fetched = await db.GetScheduleAsync("ts");
        Assert.Equal(when, fetched!.LastFiredAt);
    }

    [Fact]
    public async Task DeleteSchedule_RemovesRow()
    {
        await using var db = await CreateAsync();
        await db.CreateScheduleAsync(MakeSchedule("doomed"));
        await db.DeleteScheduleAsync("doomed");
        Assert.Null(await db.GetScheduleAsync("doomed"));
    }

    [Fact]
    public async Task ApplySchedules_ReplacesExisting_ResetsStateAndId()
    {
        await using var db = await CreateAsync();

        var original = new WorkflowSchedule(
            Id: "old-id",
            ScheduleName: "applied",
            WorkflowName: "MyApp.Workflow",
            ClassName: "MyApp",
            Cron: "0 0 * * * *",
            Status: ScheduleStatus.Paused,
            Context: null,
            LastFiredAt: DateTimeOffset.UtcNow,
            AutomaticBackfill: false,
            CronTimezone: null,
            QueueName: null);
        await db.CreateScheduleAsync(original);

        await db.ApplySchedulesAsync([original]);

        var fetched = await db.GetScheduleAsync("applied");
        Assert.NotNull(fetched);
        Assert.NotEqual("old-id", fetched!.Id);
        Assert.Equal(ScheduleStatus.Active, fetched.Status);
        Assert.Null(fetched.LastFiredAt);
    }
}
