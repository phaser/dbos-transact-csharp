using Dbos.Transact.Migrations;
using Dbos.Transact.Tests.Fixtures;
using Microsoft.Data.Sqlite;

namespace Dbos.Transact.Tests.Migrations;

public class MigrationManagerSqliteTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteFixture _fixture = new(SqliteFixture.Mode.File);

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();
    public void Dispose() { _fixture.Dispose(); GC.SuppressFinalize(this); }

    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(_fixture.ConnectionString);
        conn.Open();
        return conn;
    }

    [Fact]
    public async Task RunAsync_AppliesAllMigrations()
    {
        await using var conn = new SqliteConnection(_fixture.ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Sqlite);
        await mgr.RunAsync();

        // workflow_status must exist
        await using var check = conn.CreateCommand();
        check.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='workflow_status'";
        var result = await check.ExecuteScalarAsync();
        Assert.Equal("workflow_status", result?.ToString());
    }

    [Fact]
    public async Task RunAsync_IsIdempotent()
    {
        await using var conn = new SqliteConnection(_fixture.ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Sqlite);
        await mgr.RunAsync();
        await mgr.RunAsync(); // second run — must not throw

        await using var check = conn.CreateCommand();
        check.CommandText = "SELECT version FROM dbos_migrations";
        var version = await check.ExecuteScalarAsync();
        Assert.Equal(19L, version);
    }

    [Fact]
    public async Task RunAsync_CreatesExpectedTables()
    {
        await using var conn = new SqliteConnection(_fixture.ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Sqlite);
        await mgr.RunAsync();

        string[] expectedTables =
        [
            "workflow_status", "operation_outputs", "notifications",
            "workflow_events", "streams", "event_dispatch_kv",
            "workflow_events_history", "workflow_schedules",
            "application_versions"
        ];

        foreach (var table in expectedTables)
        {
            await using var check = conn.CreateCommand();
            check.CommandText = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{table}'";
            var result = await check.ExecuteScalarAsync();
            Assert.True(table == result?.ToString(), $"Table '{table}' should exist");
        }
    }

    [Fact]
    public async Task RunAsync_MemoryMode_Works()
    {
        var memFixture = new SqliteFixture(SqliteFixture.Mode.Memory);
        await memFixture.InitializeAsync();
        try
        {
            await using var conn = new SqliteConnection(memFixture.ConnectionString);
            var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Sqlite);
            await mgr.RunAsync();

            await using var check = conn.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table'";
            var tableCount = (long)(await check.ExecuteScalarAsync())!;
            Assert.True(tableCount >= 9, $"Expected at least 9 tables, got {tableCount}");
        }
        finally
        {
            await memFixture.DisposeAsync();
        }
    }
}
