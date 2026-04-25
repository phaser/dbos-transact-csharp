using Microsoft.Data.Sqlite;

namespace Dbos.Transact.Tests.Fixtures;

public class SqliteFixtureTests
{
    [Fact]
    public async Task FileMode_SelectOne_Succeeds()
    {
        var fixture = new SqliteFixture(SqliteFixture.Mode.File);
        await fixture.InitializeAsync();
        try
        {
            await using var conn = new SqliteConnection(fixture.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            var result = await cmd.ExecuteScalarAsync();
            Assert.Equal(1L, result);
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task MemoryMode_SelectOne_Succeeds()
    {
        var fixture = new SqliteFixture(SqliteFixture.Mode.Memory);
        await fixture.InitializeAsync();
        try
        {
            await using var conn = new SqliteConnection(fixture.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            var result = await cmd.ExecuteScalarAsync();
            Assert.Equal(1L, result);
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task FileMode_WalEnabled()
    {
        var fixture = new SqliteFixture(SqliteFixture.Mode.File);
        await fixture.InitializeAsync();
        try
        {
            await using var conn = new SqliteConnection(fixture.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode;";
            var result = (string?)await cmd.ExecuteScalarAsync();
            Assert.Equal("wal", result);
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task FileMode_TempFileRemovedOnDispose()
    {
        var fixture = new SqliteFixture(SqliteFixture.Mode.File);
        await fixture.InitializeAsync();
        var connStr = fixture.ConnectionString;
        var path = new SqliteConnectionStringBuilder(connStr).DataSource;
        Assert.True(File.Exists(path), "temp file should exist after init");
        await fixture.DisposeAsync();
        Assert.False(File.Exists(path), "temp file should be removed after dispose");
    }

    [Fact]
    public async Task MemoryMode_SharedCacheIsolation_BetweenFixtureInstances()
    {
        // Two different fixture instances use different database names → isolated
        var f1 = new SqliteFixture(SqliteFixture.Mode.Memory);
        var f2 = new SqliteFixture(SqliteFixture.Mode.Memory);
        await f1.InitializeAsync();
        await f2.InitializeAsync();
        try
        {
            Assert.NotEqual(f1.ConnectionString, f2.ConnectionString);
        }
        finally
        {
            await f1.DisposeAsync();
            await f2.DisposeAsync();
        }
    }
}
