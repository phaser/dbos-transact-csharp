using Dbos.Transact.Migrations;
using Dbos.Transact.Sqlite.Database;
using Dbos.Transact.Tests.Fixtures;
using Dbos.Transact.Workflow;
using Microsoft.Data.Sqlite;

namespace Dbos.Transact.Tests.Database;

public sealed class SqliteSystemDatabaseTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteFixture _fixture = new(SqliteFixture.Mode.File);

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();
    public void Dispose() { _fixture.Dispose(); GC.SuppressFinalize(this); }

    private async Task RunMigrationsAsync()
    {
        await using var conn = new SqliteConnection(_fixture.ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Sqlite);
        await mgr.RunAsync();
    }

    private SqliteSystemDatabase CreateDb() => new(_fixture.ConnectionString);

    [Fact]
    public async Task GetWorkflowStatusAsync_NonExistentWorkflow_ReturnsNull()
    {
        await RunMigrationsAsync();
        var db = CreateDb();
        var result = await db.GetWorkflowStatusAsync("does-not-exist");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetWorkflowSerializationAsync_NonExistentWorkflow_ReturnsNull()
    {
        await RunMigrationsAsync();
        var db = CreateDb();
        var result = await db.GetWorkflowSerializationAsync("does-not-exist");
        Assert.Null(result);
    }

    [Fact]
    public async Task ListWorkflowsAsync_EmptyDatabase_ReturnsEmptyList()
    {
        await RunMigrationsAsync();
        var db = CreateDb();
        var result = await db.ListWorkflowsAsync(new ListWorkflowsInput());
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPendingWorkflowsAsync_EmptyDatabase_ReturnsEmptyList()
    {
        await RunMigrationsAsync();
        var db = CreateDb();
        var result = await db.GetPendingWorkflowsAsync(["executor-1"], appVersion: null);
        Assert.Empty(result);
    }
}
