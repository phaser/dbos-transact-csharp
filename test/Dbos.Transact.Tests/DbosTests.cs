using Dbos.Transact.Migrations;
using Dbos.Transact.Postgres;
using Dbos.Transact.Sqlite;
using Dbos.Transact.Tests.Fixtures;
using Dbos.Transact.Workflow;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace Dbos.Transact.Tests;

// ── Test workflow types ───────────────────────────────────────────────────────

#pragma warning disable CA1812 // proxied/instantiated via reflection
file interface IDbosTestStepService
{
    [Step]
    Task<int> AddOneAsync(int value);
}

file sealed class DbosTestStepService : IDbosTestStepService
{
    public int InvocationCount { get; private set; }

    public Task<int> AddOneAsync(int value)
    {
        InvocationCount++;
        return Task.FromResult(value + 1);
    }
}

file interface IDbosTestWorkflowService
{
    Task<int> RunAsync(int n);
}

file sealed class DbosTestWorkflowService : IDbosTestWorkflowService
{
    private readonly IDbosTestStepService _steps;

    public DbosTestWorkflowService(IDbosTestStepService steps) => _steps = steps;

    [Workflow]
    public Task<int> RunAsync(int n) => _steps.AddOneAsync(n);
}
#pragma warning restore CA1812

// ── Shared parameterized logic ────────────────────────────────────────────────

file static class DbosTestScenarios
{
    public static async Task FacadeRunsRegisteredWorkflowEndToEnd(DbosBuilder builder)
    {
        var stepImpl = new DbosTestStepService();

        await using var dbos = builder
            .WithOptions(o => o with { ExecutorId = "dbos-facade-test" })
            .Build();

        var stepProxy = dbos.RegisterProxy<IDbosTestStepService>(stepImpl);
        dbos.RegisterProxy<IDbosTestWorkflowService>(new DbosTestWorkflowService(stepProxy));

        await dbos.LaunchAsync();

        var handle = await dbos.StartWorkflowAsync<int>(
            workflowName: nameof(DbosTestWorkflowService.RunAsync),
            className: typeof(DbosTestWorkflowService).FullName,
            instanceName: null,
            args: [5]);

        var result = await handle.GetResultAsync();
        Assert.Equal(6, result);
        Assert.Equal(1, stepImpl.InvocationCount);

        var status = await dbos.GetWorkflowStatusAsync(handle.WorkflowId);
        Assert.NotNull(status);
        Assert.Equal(WorkflowState.Success, status!.Status);
    }

    public static async Task RegisterAfterLaunchThrows(DbosBuilder builder)
    {
        await using var dbos = builder.Build();
        await dbos.LaunchAsync();

        Assert.Throws<InvalidOperationException>(() => dbos.RegisterQueue(new Queue("late-queue")));
        Assert.Throws<InvalidOperationException>(() =>
            dbos.RegisterProxy<IDbosTestStepService>(new DbosTestStepService()));
    }

    public static async Task StartBeforeLaunchThrows(DbosBuilder builder)
    {
        await using var dbos = builder.Build();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dbos.StartWorkflowAsync<int>("anything", "anything", null, args: []));
    }

    public static async Task LaunchIsIdempotent(DbosBuilder builder)
    {
        await using var dbos = builder.Build();
        await dbos.LaunchAsync();
        await dbos.LaunchAsync();
        Assert.True(dbos.IsLaunched);
    }
}

// ── Postgres ──────────────────────────────────────────────────────────────────

[Collection("Postgres")]
public sealed class PostgresDbosTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string Schema = "dbos_facade_pg_test";
    private readonly PostgresFixture _fixture;

    public PostgresDbosTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Postgres, Schema);
        await mgr.RunAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private DbosBuilder NewBuilder() =>
        Dbos.Builder("dbos-facade-pg-test")
            .WithOptions(o => o with { Migrate = false }) // migrations already applied above
            .UsePostgres(_fixture.ConnectionString, Schema);

    [Fact] public Task RunsRegisteredWorkflowEndToEnd() => DbosTestScenarios.FacadeRunsRegisteredWorkflowEndToEnd(NewBuilder());
    [Fact] public Task RegisterAfterLaunchThrows() => DbosTestScenarios.RegisterAfterLaunchThrows(NewBuilder());
    [Fact] public Task StartBeforeLaunchThrows() => DbosTestScenarios.StartBeforeLaunchThrows(NewBuilder());
    [Fact] public Task LaunchIsIdempotent() => DbosTestScenarios.LaunchIsIdempotent(NewBuilder());
}

// ── SQLite ────────────────────────────────────────────────────────────────────

public sealed class SqliteDbosTests : IAsyncLifetime, IDisposable
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

    private DbosBuilder NewBuilder() =>
        Dbos.Builder("dbos-facade-sqlite-test")
            .WithOptions(o => o with { Migrate = false })
            .UseSqlite(_fixture.ConnectionString);

    [Fact] public Task RunsRegisteredWorkflowEndToEnd() => DbosTestScenarios.FacadeRunsRegisteredWorkflowEndToEnd(NewBuilder());
    [Fact] public Task RegisterAfterLaunchThrows() => DbosTestScenarios.RegisterAfterLaunchThrows(NewBuilder());
    [Fact] public Task StartBeforeLaunchThrows() => DbosTestScenarios.StartBeforeLaunchThrows(NewBuilder());
    [Fact] public Task LaunchIsIdempotent() => DbosTestScenarios.LaunchIsIdempotent(NewBuilder());
}
