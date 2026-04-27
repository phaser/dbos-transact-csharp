using System.Reflection;
using Dbos.Transact.Database;
using Dbos.Transact.Execution;
using Dbos.Transact.Internal;
using Dbos.Transact.Json;
using Dbos.Transact.Migrations;
using Dbos.Transact.Postgres.Database;
using Dbos.Transact.Sqlite.Database;
using Dbos.Transact.Tests.Fixtures;
using Dbos.Transact.Workflow;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace Dbos.Transact.Tests.Execution;

// ── Shared workflow types ─────────────────────────────────────────────────────

file interface IQueueStepService
{
    [Step]
    Task<int> AddOneAsync(int value);
}

file sealed class QueueTrackingStepService : IQueueStepService
{
    public int InvocationCount { get; private set; }

    public Task<int> AddOneAsync(int value)
    {
        InvocationCount++;
        return Task.FromResult(value + 1);
    }
}

file sealed class QueueWorkflowHost
{
    public IQueueStepService Steps { get; }

    public QueueWorkflowHost(IQueueStepService steps) => Steps = steps;

    [Workflow]
    public Task<int> RunAsync(int n) => Steps.AddOneAsync(n);
}

file static class QueueWorkflowHostHelper
{
    private static readonly MethodInfo RunMethod =
        typeof(QueueWorkflowHost).GetMethod(nameof(QueueWorkflowHost.RunAsync))!;

    public static RegisteredWorkflow BuildWorkflow(IQueueStepService steps)
    {
        var target = new QueueWorkflowHost(steps);
        return new RegisteredWorkflow(
            WorkflowName: "RunAsync",
            ClassName: nameof(QueueWorkflowHost),
            InstanceName: null,
            Target: target,
            WorkflowMethod: RunMethod,
            MaxRecoveryAttempts: 3,
            SerializationStrategy: SerializationStrategy.Default);
    }
}

// ── Shared test logic ─────────────────────────────────────────────────────────

file static class QueueServiceTests
{
    public static async Task EnqueueDequeue_WorkflowCompletesViaQueue(SystemDatabase db)
    {
        var svc = new QueueTrackingStepService();
        var executor = new DbosExecutor(db, DbosJsonSerializer.Instance, "test-exec");
        var proxy = DbosProxyFactory.CreateProxy<IQueueStepService>(svc, null, executor.CreateInvocationHandler());
        var rw = QueueWorkflowHostHelper.BuildWorkflow(proxy);
        executor.RegisterWorkflow(rw);

        await using var queueService = new QueueService(executor, db);
        queueService.SetSpeedupForTest();
        var queue = new Queue("enqueue-dequeue-q");
        queueService.Start([queue], new HashSet<string>());

        var opts = new StartWorkflowOptions { QueueName = queue.Name };
        var handle = await executor.StartWorkflowAsync<int>(rw, args: [5], options: opts);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await handle.GetResultAsync(cts.Token);

        Assert.Equal(6, result);
        Assert.Equal(1, svc.InvocationCount);
    }

    public static async Task GlobalConcurrencyLimit_LimitsDequeue(SystemDatabase db)
    {
        var executor = new DbosExecutor(db, DbosJsonSerializer.Instance, "test-exec");
        var svc = new QueueTrackingStepService();
        var proxy = DbosProxyFactory.CreateProxy<IQueueStepService>(svc, null, executor.CreateInvocationHandler());
        var rw = QueueWorkflowHostHelper.BuildWorkflow(proxy);

        var queue = new Queue("concurrency-q", Concurrency: 2, WorkerConcurrency: null,
            PriorityEnabled: false, PartitioningEnabled: false, RateLimit: null);

        // Enqueue 3 workflows — each goes to ENQUEUED state, not executed yet
        for (int i = 0; i < 3; i++)
            await executor.StartWorkflowAsync<int>(rw, args: [i],
                options: new StartWorkflowOptions { QueueName = queue.Name });

        // First dequeue: 0 PENDING, global limit=2 → dequeues 2
        var dequeued1 = await db.GetAndStartQueuedWorkflowsAsync(queue, "exec-a", null, null);
        Assert.Equal(2, dequeued1.Count);

        // Second dequeue: 2 PENDING (global limit=2), available=0 → empty
        var dequeued2 = await db.GetAndStartQueuedWorkflowsAsync(queue, "exec-b", null, null);
        Assert.Empty(dequeued2);
    }

    public static async Task WorkerConcurrencyLimit_LimitsPerExecutorDequeue(SystemDatabase db)
    {
        var executor = new DbosExecutor(db, DbosJsonSerializer.Instance, "test-exec");
        var svc = new QueueTrackingStepService();
        var proxy = DbosProxyFactory.CreateProxy<IQueueStepService>(svc, null, executor.CreateInvocationHandler());
        var rw = QueueWorkflowHostHelper.BuildWorkflow(proxy);

        var queue = new Queue("worker-concurrency-q", Concurrency: null, WorkerConcurrency: 1,
            PriorityEnabled: false, PartitioningEnabled: false, RateLimit: null);

        // Enqueue 3 workflows
        for (int i = 0; i < 3; i++)
            await executor.StartWorkflowAsync<int>(rw, args: [i],
                options: new StartWorkflowOptions { QueueName = queue.Name });

        // exec-a dequeues 1 (workerConcurrency=1, localPending=0 → maxTasks=1)
        var dequeued1 = await db.GetAndStartQueuedWorkflowsAsync(queue, "exec-a", null, null);
        Assert.Single(dequeued1);

        // exec-a tries again: 1 PENDING for exec-a, maxTasks = max(0, 1-1) = 0 → empty
        var dequeued2 = await db.GetAndStartQueuedWorkflowsAsync(queue, "exec-a", null, null);
        Assert.Empty(dequeued2);

        // exec-b has 0 PENDING, maxTasks = 1 → dequeues 1
        var dequeued3 = await db.GetAndStartQueuedWorkflowsAsync(queue, "exec-b", null, null);
        Assert.Single(dequeued3);
    }
}

// ── Postgres ──────────────────────────────────────────────────────────────────

[Collection("Postgres")]
public sealed class PostgresQueueServiceTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string Schema = "dbos_queue_pg_test";
    private readonly PostgresFixture _fixture;

    public PostgresQueueServiceTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Postgres, Schema);
        await mgr.RunAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private PostgresSystemDatabase OpenDb() => new(_fixture.ConnectionString, Schema);

    [Fact]
    public async Task EnqueueDequeue_WorkflowCompletesViaQueue()
    {
        await using var db = OpenDb();
        await QueueServiceTests.EnqueueDequeue_WorkflowCompletesViaQueue(db);
    }

    [Fact]
    public async Task GlobalConcurrencyLimit_LimitsDequeue()
    {
        await using var db = OpenDb();
        await QueueServiceTests.GlobalConcurrencyLimit_LimitsDequeue(db);
    }

    [Fact]
    public async Task WorkerConcurrencyLimit_LimitsPerExecutorDequeue()
    {
        await using var db = OpenDb();
        await QueueServiceTests.WorkerConcurrencyLimit_LimitsPerExecutorDequeue(db);
    }

    /// <summary>PG-only: two workers racing for the same queue claim distinct items via SKIP LOCKED.</summary>
    [Fact]
    public async Task SkipLocked_MultipleWorkers_ClaimDistinctItems()
    {
        await using var db = OpenDb();
        var executor = new DbosExecutor(db, DbosJsonSerializer.Instance, "test-exec");
        var svc = new QueueTrackingStepService();
        var proxy = DbosProxyFactory.CreateProxy<IQueueStepService>(svc, null, executor.CreateInvocationHandler());
        var rw = QueueWorkflowHostHelper.BuildWorkflow(proxy);

        var queue = new Queue("skip-locked-q"); // no global concurrency → uses SKIP LOCKED

        // Enqueue 4 workflows
        for (int i = 0; i < 4; i++)
            await executor.StartWorkflowAsync<int>(rw, args: [i],
                options: new StartWorkflowOptions { QueueName = queue.Name });

        // Two executors dequeue concurrently — each should claim distinct items
        var task1 = db.GetAndStartQueuedWorkflowsAsync(queue, "worker-1", null, null);
        var task2 = db.GetAndStartQueuedWorkflowsAsync(queue, "worker-2", null, null);
        var (dequeued1, dequeued2) = (await task1, await task2);

        var allIds = dequeued1.Concat(dequeued2).ToList();
        Assert.Equal(4, allIds.Count);
        Assert.Equal(4, allIds.Distinct().Count()); // no duplicates
    }
}

// ── SQLite ────────────────────────────────────────────────────────────────────

public sealed class SqliteQueueServiceTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteFixture _fixture = new(SqliteFixture.Mode.File);

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
        await using var conn = new SqliteConnection(_fixture.ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Sqlite);
        await mgr.RunAsync();
    }

    public Task DisposeAsync() => _fixture.DisposeAsync();
    public void Dispose() { _fixture.Dispose(); GC.SuppressFinalize(this); }

    private SqliteSystemDatabase OpenDb() => new(_fixture.ConnectionString);

    [Fact]
    public async Task EnqueueDequeue_WorkflowCompletesViaQueue()
    {
        await using var db = OpenDb();
        await QueueServiceTests.EnqueueDequeue_WorkflowCompletesViaQueue(db);
    }

    [Fact]
    public async Task GlobalConcurrencyLimit_LimitsDequeue()
    {
        await using var db = OpenDb();
        await QueueServiceTests.GlobalConcurrencyLimit_LimitsDequeue(db);
    }

    [Fact]
    public async Task WorkerConcurrencyLimit_LimitsPerExecutorDequeue()
    {
        await using var db = OpenDb();
        await QueueServiceTests.WorkerConcurrencyLimit_LimitsPerExecutorDequeue(db);
    }

    /// <summary>SQLite-only: BEGIN IMMEDIATE serializes concurrent dequeue — no double-claim.</summary>
    [Fact]
    public async Task BeginImmediate_ConcurrentDequeue_NoDoubleClaim()
    {
        await using var db = OpenDb();
        var executor = new DbosExecutor(db, DbosJsonSerializer.Instance, "test-exec");
        var svc = new QueueTrackingStepService();
        var proxy = DbosProxyFactory.CreateProxy<IQueueStepService>(svc, null, executor.CreateInvocationHandler());
        var rw = QueueWorkflowHostHelper.BuildWorkflow(proxy);

        var queue = new Queue("no-double-claim-q");

        // Enqueue 1 workflow
        await executor.StartWorkflowAsync<int>(rw, args: [7],
            options: new StartWorkflowOptions { QueueName = queue.Name });

        // Two concurrent dequeue attempts — only one should claim the single item
        await using var db2 = OpenDb();
        var task1 = db.GetAndStartQueuedWorkflowsAsync(queue, "worker-1", null, null);
        var task2 = db2.GetAndStartQueuedWorkflowsAsync(queue, "worker-2", null, null);
        var (ids1, ids2) = (await task1, await task2);

        var total = ids1.Count + ids2.Count;
        Assert.Equal(1, total); // exactly one worker claimed the item
    }
}
