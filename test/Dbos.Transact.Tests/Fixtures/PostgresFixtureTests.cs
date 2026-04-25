using Npgsql;

namespace Dbos.Transact.Tests.Fixtures;

[Collection("Postgres")]
public class PostgresFixtureTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public PostgresFixtureTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ConnectionString_IsNonEmpty()
    {
        Assert.NotEmpty(_fixture.ConnectionString);
    }

    [Fact]
    public async Task SelectOne_Succeeds()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1";
        var result = await cmd.ExecuteScalarAsync();
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task MultipleConnections_SameContainer()
    {
        await using var conn1 = new NpgsqlConnection(_fixture.ConnectionString);
        await using var conn2 = new NpgsqlConnection(_fixture.ConnectionString);
        await conn1.OpenAsync();
        await conn2.OpenAsync();
        await using var cmd = conn1.CreateCommand();
        cmd.CommandText = "SELECT current_database()";
        var db = (string?)await cmd.ExecuteScalarAsync();
        Assert.Equal("dbos_test", db);
    }
}
