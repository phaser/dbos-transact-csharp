using Dbos.Transact.Migrations;
using Dbos.Transact.Postgres.Database;
using Dbos.Transact.Tests.Fixtures;
using Dbos.Transact.Workflow;
using Npgsql;

namespace Dbos.Transact.Tests.Database;

[Collection("Postgres")]
public sealed class PostgresSystemDatabaseTests : IClassFixture<PostgresFixture>
{
    private const string Schema = "dbos_sysdb_pg_test";

    private readonly PostgresFixture _fixture;

    public PostgresSystemDatabaseTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task RunMigrationsAsync()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Postgres, Schema);
        await mgr.RunAsync();
    }

    [Fact]
    public async Task GetWorkflowStatusAsync_NonExistentWorkflow_ReturnsNull()
    {
        await RunMigrationsAsync();
        await using var db = new PostgresSystemDatabase(_fixture.ConnectionString, Schema);
        var result = await db.GetWorkflowStatusAsync("does-not-exist");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetWorkflowSerializationAsync_NonExistentWorkflow_ReturnsNull()
    {
        await RunMigrationsAsync();
        await using var db = new PostgresSystemDatabase(_fixture.ConnectionString, Schema);
        var result = await db.GetWorkflowSerializationAsync("does-not-exist");
        Assert.Null(result);
    }

    [Fact]
    public async Task ListWorkflowsAsync_EmptyDatabase_ReturnsEmptyList()
    {
        await RunMigrationsAsync();
        await using var db = new PostgresSystemDatabase(_fixture.ConnectionString, Schema);
        var result = await db.ListWorkflowsAsync(new ListWorkflowsInput());
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPendingWorkflowsAsync_EmptyDatabase_ReturnsEmptyList()
    {
        await RunMigrationsAsync();
        await using var db = new PostgresSystemDatabase(_fixture.ConnectionString, Schema);
        var result = await db.GetPendingWorkflowsAsync(["executor-1"], appVersion: null);
        Assert.Empty(result);
    }
}
