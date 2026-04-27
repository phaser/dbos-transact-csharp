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

file interface ITestStepService
{
    [Step]
    Task<int> AddOneAsync(int value);
}

// Separate interface that enables retries with zero delay for fast tests
file interface IRetryableStepService
{
    [Step(RetriesAllowed = true, MaxAttempts = 3, IntervalSeconds = 0)]
    Task<int> AddOneAsync(int value);
}

file sealed class TrackingStepService : ITestStepService
{
    public int InvocationCount { get; private set; }

    public Task<int> AddOneAsync(int value)
    {
        InvocationCount++;
        return Task.FromResult(value + 1);
    }
}

file sealed class FailThenSucceedStepService : IRetryableStepService
{
    private readonly int _failCount;
    private int _calls;

    public FailThenSucceedStepService(int failCount) => _failCount = failCount;

    public Task<int> AddOneAsync(int value)
    {
        _calls++;
        if (_calls <= _failCount)
            throw new InvalidOperationException($"transient failure #{_calls}");
        return Task.FromResult(value + 1);
    }
}

file sealed class AlwaysThrowStepService : ITestStepService
{
    public Task<int> AddOneAsync(int value) =>
        throw new InvalidOperationException("step always fails");
}

file sealed class WorkflowHost
{
    public ITestStepService Steps { get; }

    public WorkflowHost(ITestStepService steps) => Steps = steps;

    [Workflow]
    public Task<int> RunAsync(int n) => Steps.AddOneAsync(n);
}

file sealed class RetryableWorkflowHost
{
    public IRetryableStepService Steps { get; }

    public RetryableWorkflowHost(IRetryableStepService steps) => Steps = steps;

    [Workflow]
    public Task<int> RunAsync(int n) => Steps.AddOneAsync(n);
}

file static class WorkflowHostHelper
{
    private static readonly MethodInfo RunAsyncMethod =
        typeof(WorkflowHost).GetMethod(nameof(WorkflowHost.RunAsync))!;

    private static readonly MethodInfo RetryableRunAsyncMethod =
        typeof(RetryableWorkflowHost).GetMethod(nameof(RetryableWorkflowHost.RunAsync))!;

    public static RegisteredWorkflow BuildWorkflow(
        ITestStepService steps,
        string workflowName = "RunAsync",
        int maxRecoveryAttempts = 3)
    {
        var target = new WorkflowHost(steps);
        return new RegisteredWorkflow(
            WorkflowName: workflowName,
            ClassName: nameof(WorkflowHost),
            InstanceName: null,
            Target: target,
            WorkflowMethod: RunAsyncMethod,
            MaxRecoveryAttempts: maxRecoveryAttempts,
            SerializationStrategy: SerializationStrategy.Default);
    }

    public static RegisteredWorkflow BuildRetryableWorkflow(
        IRetryableStepService steps,
        string workflowName = "RunAsync",
        int maxRecoveryAttempts = 3)
    {
        var target = new RetryableWorkflowHost(steps);
        return new RegisteredWorkflow(
            WorkflowName: workflowName,
            ClassName: nameof(RetryableWorkflowHost),
            InstanceName: null,
            Target: target,
            WorkflowMethod: RetryableRunAsyncMethod,
            MaxRecoveryAttempts: maxRecoveryAttempts,
            SerializationStrategy: SerializationStrategy.Default);
    }
}

// ── Shared test logic ─────────────────────────────────────────────────────────

file static class DbosExecutorTests
{
    public static async Task WorkflowRunsToCompletion(SystemDatabase db)
    {
        var svc = new TrackingStepService();
        var executor = new DbosExecutor(db, DbosJsonSerializer.Instance, executorId: "test-exec");
        var proxy = DbosProxyFactory.CreateProxy<ITestStepService>(svc, null, executor.CreateInvocationHandler());
        var rw = WorkflowHostHelper.BuildWorkflow(proxy);

        var handle = await executor.StartWorkflowAsync<int>(rw, args: [5]);
        var result = await handle.GetResultAsync();

        Assert.Equal(6, result);
        Assert.Equal(1, svc.InvocationCount);
    }

    public static async Task WorkflowStep_Throws_ExceptionPropagated(SystemDatabase db)
    {
        var svc = new AlwaysThrowStepService();
        var executor = new DbosExecutor(db, DbosJsonSerializer.Instance, executorId: "test-exec");
        var proxy = DbosProxyFactory.CreateProxy<ITestStepService>(svc, null, executor.CreateInvocationHandler());
        var rw = WorkflowHostHelper.BuildWorkflow(proxy);

        var handle = await executor.StartWorkflowAsync<int>(rw, args: [1]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handle.GetResultAsync());
    }

    public static async Task Workflow_DuplicateSubmission_ReturnsExistingResult(SystemDatabase db)
    {
        var svc = new TrackingStepService();
        var executor = new DbosExecutor(db, DbosJsonSerializer.Instance, executorId: "test-exec");
        var proxy = DbosProxyFactory.CreateProxy<ITestStepService>(svc, null, executor.CreateInvocationHandler());
        var workflowId = Guid.NewGuid().ToString();
        var opts = new StartWorkflowOptions { WorkflowId = workflowId };

        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // First submission — runs the workflow
        var rw1 = WorkflowHostHelper.BuildWorkflow(proxy);
        var handle1 = await executor.StartWorkflowAsync<int>(rw1, args: [10], options: opts);
        var result1 = await handle1.GetResultAsync(cts1.Token);
        Assert.Equal(11, result1);
        Assert.Equal(1, svc.InvocationCount);

        // Second submission with same ID — sees already-completed, returns DB poll handle
        var rw2 = WorkflowHostHelper.BuildWorkflow(proxy);
        var handle2 = await executor.StartWorkflowAsync<int>(rw2, args: [10], options: opts);
        var result2 = await handle2.GetResultAsync(cts2.Token);
        Assert.Equal(11, result2);
        Assert.Equal(1, svc.InvocationCount);
    }

    public static async Task WorkflowStep_WithRetries_EventuallySucceeds(SystemDatabase db)
    {
        var svc = new FailThenSucceedStepService(failCount: 2);
        var executor = new DbosExecutor(db, DbosJsonSerializer.Instance, executorId: "test-exec");
        var proxy = DbosProxyFactory.CreateProxy<IRetryableStepService>(svc, null, executor.CreateInvocationHandler());
        var rw = WorkflowHostHelper.BuildRetryableWorkflow(proxy);

        var handle = await executor.StartWorkflowAsync<int>(rw, args: [7]);
        var result = await handle.GetResultAsync();

        Assert.Equal(8, result);
    }
}

// ── Postgres ──────────────────────────────────────────────────────────────────

[Collection("Postgres")]
public sealed class PostgresDbosExecutorTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string Schema = "dbos_exec_pg_test";

    private readonly PostgresFixture _fixture;

    public PostgresDbosExecutorTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Postgres, Schema);
        await mgr.RunAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private PostgresSystemDatabase OpenDb() => new(_fixture.ConnectionString, Schema);

    [Fact]
    public async Task WorkflowRunsToCompletion_ReturnsResult()
    {
        await using var db = OpenDb();
        await DbosExecutorTests.WorkflowRunsToCompletion(db);
    }

    [Fact]
    public async Task WorkflowStep_Throws_ExceptionPropagated()
    {
        await using var db = OpenDb();
        await DbosExecutorTests.WorkflowStep_Throws_ExceptionPropagated(db);
    }

    [Fact]
    public async Task Workflow_DuplicateSubmission_ReturnsExistingResult()
    {
        await using var db = OpenDb();
        await DbosExecutorTests.Workflow_DuplicateSubmission_ReturnsExistingResult(db);
    }

    [Fact]
    public async Task WorkflowStep_WithRetries_EventuallySucceeds()
    {
        await using var db = OpenDb();
        await DbosExecutorTests.WorkflowStep_WithRetries_EventuallySucceeds(db);
    }
}

// ── SQLite ────────────────────────────────────────────────────────────────────

public sealed class SqliteDbosExecutorTests : IAsyncLifetime, IDisposable
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
    public async Task WorkflowRunsToCompletion_ReturnsResult()
    {
        await using var db = OpenDb();
        await DbosExecutorTests.WorkflowRunsToCompletion(db);
    }

    [Fact]
    public async Task WorkflowStep_Throws_ExceptionPropagated()
    {
        await using var db = OpenDb();
        await DbosExecutorTests.WorkflowStep_Throws_ExceptionPropagated(db);
    }

    [Fact]
    public async Task Workflow_DuplicateSubmission_ReturnsExistingResult()
    {
        await using var db = OpenDb();
        await DbosExecutorTests.Workflow_DuplicateSubmission_ReturnsExistingResult(db);
    }

    [Fact]
    public async Task WorkflowStep_WithRetries_EventuallySucceeds()
    {
        await using var db = OpenDb();
        await DbosExecutorTests.WorkflowStep_WithRetries_EventuallySucceeds(db);
    }
}
