using Dbos.Transact.Database;
using Dbos.Transact.Migrations;
using Dbos.Transact.Postgres;
using Dbos.Transact.Postgres.Database;
using Dbos.Transact.Sqlite;
using Dbos.Transact.Sqlite.Database;
using Dbos.Transact.Tests.Fixtures;
using Dbos.Transact.Workflow;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace Dbos.Transact.Tests;

#pragma warning disable CA1812 // proxied/instantiated via reflection
file interface IClientTestStepService
{
    [Step]
    Task<int> AddOneAsync(int value);
}

file sealed class ClientTestStepService : IClientTestStepService
{
    public Task<int> AddOneAsync(int value) => Task.FromResult(value + 1);
}

file interface IClientTestWorkflowService
{
    Task<int> RunAsync(int n);
}

file sealed class ClientTestWorkflowService : IClientTestWorkflowService
{
    private readonly IClientTestStepService _steps;

    public ClientTestWorkflowService(IClientTestStepService steps) => _steps = steps;

    [Workflow]
    public Task<int> RunAsync(int n) => _steps.AddOneAsync(n);
}
#pragma warning restore CA1812

// ── Shared parameterized logic ────────────────────────────────────────────────

file static class DbosClientScenarios
{
    /// <summary>Runs a workflow via the in-process facade, then uses a client on a fresh
    /// system-database connection to read the same workflow's status and result.</summary>
    public static async Task ClientObservesWorkflowFromSeparateConnection(
        DbosBuilder builder,
        SystemDatabase clientSystemDatabase)
    {
        var stepImpl = new ClientTestStepService();

        await using var dbos = builder
            .WithOptions(o => o with { ExecutorId = "dbos-client-test" })
            .Build();

        var stepProxy = dbos.RegisterProxy<IClientTestStepService>(stepImpl);
        dbos.RegisterProxy<IClientTestWorkflowService>(new ClientTestWorkflowService(stepProxy));

        await dbos.LaunchAsync();

        var handle = await dbos.StartWorkflowAsync<int>(
            workflowName: nameof(ClientTestWorkflowService.RunAsync),
            className: typeof(ClientTestWorkflowService).FullName,
            instanceName: null,
            args: [11]);

        var result = await handle.GetResultAsync();
        Assert.Equal(12, result);

        await using var client = new DbosClient(clientSystemDatabase, ownsSystemDatabase: true);

        var status = await client.GetWorkflowStatusAsync(handle.WorkflowId);
        Assert.NotNull(status);
        Assert.Equal(WorkflowState.Success, status!.Status);

        var clientResult = await client.GetResultAsync<int>(handle.WorkflowId);
        Assert.Equal(12, clientResult);

        var listed = await client.ListWorkflowsAsync(new ListWorkflowsInput(handle.WorkflowId));
        Assert.Single(listed);
        Assert.Equal(handle.WorkflowId, listed[0].WorkflowId);

        var steps = await client.ListWorkflowStepsAsync(handle.WorkflowId);
        Assert.NotEmpty(steps);
    }
}

// ── Postgres ──────────────────────────────────────────────────────────────────

[Collection("Postgres")]
public sealed class PostgresDbosClientTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string Schema = "dbos_client_pg_test";
    private readonly PostgresFixture _fixture;

    public PostgresDbosClientTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Postgres, Schema);
        await mgr.RunAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public Task ClientObservesWorkflow()
    {
        var builder = Dbos.Builder("dbos-client-pg")
            .WithOptions(o => o with { Migrate = false })
            .UsePostgres(_fixture.ConnectionString, Schema);
        var client = new PostgresSystemDatabase(_fixture.ConnectionString, Schema);
        return DbosClientScenarios.ClientObservesWorkflowFromSeparateConnection(builder, client);
    }
}

// ── SQLite ────────────────────────────────────────────────────────────────────

public sealed class SqliteDbosClientTests : IAsyncLifetime, IDisposable
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

    [Fact]
    public Task ClientObservesWorkflow()
    {
        var builder = Dbos.Builder("dbos-client-sqlite")
            .WithOptions(o => o with { Migrate = false })
            .UseSqlite(_fixture.ConnectionString);
        var client = new SqliteSystemDatabase(_fixture.ConnectionString);
        return DbosClientScenarios.ClientObservesWorkflowFromSeparateConnection(builder, client);
    }
}
