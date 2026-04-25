using Dbos.Transact.Migrations;
using Dbos.Transact.Tests.Fixtures;
using Npgsql;

namespace Dbos.Transact.Tests.Migrations;

[Collection("Postgres")]
public class MigrationManagerPostgresTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public MigrationManagerPostgresTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task RunAsync_AppliesAllMigrations()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Postgres, schema: "dbos_pg_test");
        await mgr.RunAsync();

        await using var check = conn.CreateCommand();
        check.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'dbos_pg_test' AND table_name = 'workflow_status'";
        var result = await check.ExecuteScalarAsync();
        Assert.Equal("workflow_status", result?.ToString());
    }

    [Fact]
    public async Task RunAsync_IsIdempotent()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Postgres, schema: "dbos_idem_test");
        await mgr.RunAsync();
        await mgr.RunAsync(); // second run — must not throw

        await using var check = conn.CreateCommand();
        check.CommandText = "SELECT version FROM \"dbos_idem_test\".dbos_migrations";
        var version = await check.ExecuteScalarAsync();
        Assert.Equal(19L, version);
    }

    [Fact]
    public async Task RunAsync_CreatesExpectedTables()
    {
        const string schema = "dbos_tables_test";
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Postgres, schema);
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
            check.CommandText = $"SELECT table_name FROM information_schema.tables WHERE table_schema = '{schema}' AND table_name = '{table}'";
            var result = await check.ExecuteScalarAsync();
            Assert.True(table == result?.ToString(), $"Table '{table}' should exist in schema '{schema}'");
        }
    }
}
