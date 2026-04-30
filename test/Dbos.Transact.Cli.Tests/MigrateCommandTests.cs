using Microsoft.Data.Sqlite;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Dbos.Transact.Cli.Tests;

public class MigrateCommandTests
{
    [Fact]
    public async Task Migrate_Sqlite_CreatesDbosTables()
    {
        using var fixture = new SqliteFixture();

        var r = await CliRunner.RunAsync("migrate", "--db-url", fixture.ConnectionString);
        Assert.True(r.ExitCode == 0, $"exitCode={r.ExitCode}, stderr={r.Stderr}, stdout={r.Stdout}");

        // Verify the migration version table exists and a few of the system tables.
        await using var conn = new SqliteConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
        var tables = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) tables.Add(reader.GetString(0));

        Assert.Contains("dbos_migrations", tables);
        Assert.Contains("workflow_status", tables);
        Assert.Contains("operation_outputs", tables);
    }

    [Fact]
    public async Task Migrate_Sqlite_OutputMentionsDialectAndConnection()
    {
        using var fixture = new SqliteFixture();

        var r = await CliRunner.RunAsync("migrate", "--db-url", fixture.ConnectionString);
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("Sqlite", r.Stdout, StringComparison.Ordinal);
        Assert.Contains("Migrations applied successfully.", r.Stdout, StringComparison.Ordinal);
    }
}

[Collection("Postgres")]
public sealed class PostgresMigrateCommandTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithDatabase("dbos_test")
        .WithUsername("dbos")
        .WithPassword("dbos")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task Migrate_Postgres_CreatesDbosTables()
    {
        const string schema = "dbos_cli_pg";
        var r = await CliRunner.RunAsync(
            "migrate",
            "--db-url", _container.GetConnectionString(),
            "--schema", schema);
        Assert.True(r.ExitCode == 0, $"exitCode={r.ExitCode}, stderr={r.Stderr}, stdout={r.Stdout}");
        Assert.Contains("Postgres", r.Stdout, StringComparison.Ordinal);
        Assert.Contains(schema, r.Stdout, StringComparison.Ordinal);

        await using var conn = new NpgsqlConnection(_container.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"SELECT table_name FROM information_schema.tables WHERE table_schema = '{schema}' ORDER BY table_name";
        var tables = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) tables.Add(reader.GetString(0));

        Assert.Contains("dbos_migrations", tables);
        Assert.Contains("workflow_status", tables);
        Assert.Contains("operation_outputs", tables);
    }
}
