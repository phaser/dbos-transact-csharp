using System.Reflection;
using Dbos.Transact.Database;
using Dbos.Transact.Execution;
using Dbos.Transact.Json;
using Dbos.Transact.Migrations;
using Dbos.Transact.Sqlite.Database;
using Dbos.Transact.Tests.Fixtures;
using Dbos.Transact.Workflow;
using Microsoft.Data.Sqlite;

namespace Dbos.Transact.Tests.Execution;

// ── Scheduled workflow targets ────────────────────────────────────────────────

#pragma warning disable CA1812 // Instantiated via reflection by RegisteredWorkflow
file sealed class ScheduledWorkflowHost
{
    public int InvocationCount;

    [Workflow]
    public Task RunAsync(DateTimeOffset scheduledAt, object? context)
    {
        Interlocked.Increment(ref InvocationCount);
        return Task.CompletedTask;
    }
}

file sealed class AnnotatedScheduledHost
{
    public int InvocationCount;

    [Workflow]
    [Scheduled("* * * * * *")]
    public Task RunAsync(DateTimeOffset scheduledAt, DateTimeOffset firedAt)
    {
        Interlocked.Increment(ref InvocationCount);
        return Task.CompletedTask;
    }
}
#pragma warning restore CA1812

file static class SchedulerTestHelper
{
    public static RegisteredWorkflow BuildScheduledWorkflow(ScheduledWorkflowHost host) => new(
        WorkflowName: nameof(ScheduledWorkflowHost.RunAsync),
        ClassName: nameof(ScheduledWorkflowHost),
        InstanceName: null,
        Target: host,
        WorkflowMethod: typeof(ScheduledWorkflowHost).GetMethod(nameof(ScheduledWorkflowHost.RunAsync))!,
        MaxRecoveryAttempts: 3,
        SerializationStrategy: SerializationStrategy.Default);

    public static RegisteredWorkflow BuildAnnotatedWorkflow(AnnotatedScheduledHost host) => new(
        WorkflowName: nameof(AnnotatedScheduledHost.RunAsync),
        ClassName: nameof(AnnotatedScheduledHost),
        InstanceName: null,
        Target: host,
        WorkflowMethod: typeof(AnnotatedScheduledHost).GetMethod(nameof(AnnotatedScheduledHost.RunAsync))!,
        MaxRecoveryAttempts: 3,
        SerializationStrategy: SerializationStrategy.Default);

    public static async Task<bool> WaitForAsync(Func<Task<bool>> predicate, TimeSpan timeout, TimeSpan pollEvery)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await predicate().ConfigureAwait(false)) return true;
            await Task.Delay(pollEvery).ConfigureAwait(false);
        }
        return false;
    }
}

// ── SQLite-backed tests ──────────────────────────────────────────────────────

public sealed class SqliteSchedulerServiceTests : IAsyncLifetime, IDisposable
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

    [Fact]
    public async Task DbSchedule_FiresAndUpdatesLastFiredAt()
    {
        await using var db = await CreateAsync();
        var executor = new DbosExecutor(db, DbosJsonSerializer.Instance, executorId: "test");

        var host = new ScheduledWorkflowHost();
        var rw = SchedulerTestHelper.BuildScheduledWorkflow(host);
        executor.RegisterWorkflow(rw);

        var schedule = new WorkflowSchedule(
            scheduleName: "sched-fires",
            workflowName: rw.WorkflowName,
            className: rw.ClassName,
            cron: "* * * * * *");
        await db.CreateScheduleAsync(schedule);

        await using var scheduler = new SchedulerService(executor, db, pollingInterval: TimeSpan.FromMilliseconds(200));
        scheduler.Start();

        // Within 4 seconds, expect a workflow_status row whose name matches and last_fired_at to be set.
        var fired = await SchedulerTestHelper.WaitForAsync(async () =>
        {
            var rows = await db.ListWorkflowsAsync(new ListWorkflowsInput(
                WorkflowIds: null, Status: null, StartTime: null, EndTime: null,
                WorkflowName: [nameof(ScheduledWorkflowHost.RunAsync)],
                ClassName: null, InstanceName: null, ApplicationVersion: null,
                AuthenticatedUser: null, Limit: null, Offset: null, SortDesc: null,
                WorkflowIdPrefix: ["sched-sched-fires-"],
                LoadInput: null, LoadOutput: null, QueueName: null, QueuesOnly: null,
                ExecutorIds: null, ForkedFrom: null, ParentWorkflowId: null,
                WasForkedFrom: null, HasParent: null));
            return rows.Count > 0;
        }, TimeSpan.FromSeconds(4), TimeSpan.FromMilliseconds(100));

        Assert.True(fired, "Expected at least one fire within 4 seconds");

        var refreshed = await db.GetScheduleAsync("sched-fires");
        Assert.NotNull(refreshed!.LastFiredAt);
    }

    [Fact]
    public async Task PausedScheduler_DoesNotFire()
    {
        await using var db = await CreateAsync();
        var executor = new DbosExecutor(db, DbosJsonSerializer.Instance, executorId: "test");

        var host = new ScheduledWorkflowHost();
        var rw = SchedulerTestHelper.BuildScheduledWorkflow(host);
        executor.RegisterWorkflow(rw);

        await db.CreateScheduleAsync(new WorkflowSchedule(
            scheduleName: "sched-paused",
            workflowName: rw.WorkflowName,
            className: rw.ClassName,
            cron: "* * * * * *"));

        await using var scheduler = new SchedulerService(executor, db, pollingInterval: TimeSpan.FromMilliseconds(200));
        scheduler.Pause();
        scheduler.Start();

        await Task.Delay(TimeSpan.FromSeconds(2));

        var rows = await db.ListWorkflowsAsync(new ListWorkflowsInput(
            WorkflowIds: null, Status: null, StartTime: null, EndTime: null,
            WorkflowName: [nameof(ScheduledWorkflowHost.RunAsync)],
            ClassName: null, InstanceName: null, ApplicationVersion: null,
            AuthenticatedUser: null, Limit: null, Offset: null, SortDesc: null,
            WorkflowIdPrefix: ["sched-sched-paused-"],
            LoadInput: null, LoadOutput: null, QueueName: null, QueuesOnly: null,
            ExecutorIds: null, ForkedFrom: null, ParentWorkflowId: null,
            WasForkedFrom: null, HasParent: null));

        Assert.Empty(rows);
    }

    [Fact]
    public async Task DeactivatedSchedule_StopsFiring()
    {
        await using var db = await CreateAsync();
        var executor = new DbosExecutor(db, DbosJsonSerializer.Instance, executorId: "test");

        var host = new ScheduledWorkflowHost();
        var rw = SchedulerTestHelper.BuildScheduledWorkflow(host);
        executor.RegisterWorkflow(rw);

        await db.CreateScheduleAsync(new WorkflowSchedule(
            scheduleName: "sched-deactivate",
            workflowName: rw.WorkflowName,
            className: rw.ClassName,
            cron: "* * * * * *"));

        var pollingInterval = TimeSpan.FromMilliseconds(200);
        await using var scheduler = new SchedulerService(executor, db, pollingInterval: pollingInterval);
        scheduler.Start();

        // Let it fire at least once.
        await SchedulerTestHelper.WaitForAsync(async () =>
        {
            var s = await db.GetScheduleAsync("sched-deactivate");
            return s?.LastFiredAt is not null;
        }, TimeSpan.FromSeconds(4), TimeSpan.FromMilliseconds(100));

        await db.PauseScheduleAsync("sched-deactivate"); // sets status = PAUSED → IsActive = false
        // Wait two polling intervals to ensure the runner is canceled.
        await Task.Delay(TimeSpan.FromMilliseconds(pollingInterval.TotalMilliseconds * 2 + 200));

        var firstSnapshot = await db.GetScheduleAsync("sched-deactivate");
        var firstFireCount = await CountFiresAsync(db, "sched-sched-deactivate-");
        await Task.Delay(TimeSpan.FromMilliseconds(1500)); // beyond what one cron tick would produce
        var secondFireCount = await CountFiresAsync(db, "sched-sched-deactivate-");

        // After deactivation no new fires should be recorded.
        Assert.Equal(firstFireCount, secondFireCount);
        // last_fired_at should not advance after deactivation either.
        var secondSnapshot = await db.GetScheduleAsync("sched-deactivate");
        Assert.Equal(firstSnapshot!.LastFiredAt, secondSnapshot!.LastFiredAt);
    }

    [Fact]
    public async Task AnnotatedSchedule_Fires()
    {
        await using var db = await CreateAsync();
        var executor = new DbosExecutor(db, DbosJsonSerializer.Instance, executorId: "test");

        var host = new AnnotatedScheduledHost();
        var rw = SchedulerTestHelper.BuildAnnotatedWorkflow(host);
        executor.RegisterWorkflow(rw);

        await using var scheduler = new SchedulerService(executor, db, pollingInterval: TimeSpan.FromMilliseconds(200));
        scheduler.Start();

        var fired = await SchedulerTestHelper.WaitForAsync(async () =>
        {
            var rows = await db.ListWorkflowsAsync(new ListWorkflowsInput(
                WorkflowIds: null, Status: null, StartTime: null, EndTime: null,
                WorkflowName: [nameof(AnnotatedScheduledHost.RunAsync)],
                ClassName: null, InstanceName: null, ApplicationVersion: null,
                AuthenticatedUser: null, Limit: null, Offset: null, SortDesc: null,
                WorkflowIdPrefix: ["sched-"],
                LoadInput: null, LoadOutput: null, QueueName: null, QueuesOnly: null,
                ExecutorIds: null, ForkedFrom: null, ParentWorkflowId: null,
                WasForkedFrom: null, HasParent: null));
            return rows.Count > 0;
        }, TimeSpan.FromSeconds(4), TimeSpan.FromMilliseconds(100));

        Assert.True(fired, "Expected the annotated schedule to fire within 4 seconds");
    }

    private static async Task<int> CountFiresAsync(SystemDatabase db, string prefix)
    {
        var rows = await db.ListWorkflowsAsync(new ListWorkflowsInput(
            WorkflowIds: null, Status: null, StartTime: null, EndTime: null,
            WorkflowName: null, ClassName: null, InstanceName: null,
            ApplicationVersion: null, AuthenticatedUser: null, Limit: null,
            Offset: null, SortDesc: null,
            WorkflowIdPrefix: [prefix],
            LoadInput: null, LoadOutput: null, QueueName: null, QueuesOnly: null,
            ExecutorIds: null, ForkedFrom: null, ParentWorkflowId: null,
            WasForkedFrom: null, HasParent: null));
        return rows.Count;
    }
}
