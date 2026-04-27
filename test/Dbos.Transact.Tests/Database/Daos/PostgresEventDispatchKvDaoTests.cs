using Dbos.Transact.Database;
using Dbos.Transact.Migrations;
using Dbos.Transact.Postgres.Database;
using Dbos.Transact.Tests.Fixtures;
using Npgsql;

namespace Dbos.Transact.Tests.Database.Daos;

[Collection("Postgres")]
public sealed class PostgresEventDispatchKvDaoTests : IClassFixture<PostgresFixture>
{
    private const string Schema = "dbos_kv_pg_test";

    private readonly PostgresFixture _fixture;

    public PostgresEventDispatchKvDaoTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<SystemDatabase> CreateAsync()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Postgres, Schema);
        await mgr.RunAsync();

        var db = new PostgresSystemDatabase(_fixture.ConnectionString, Schema);
        await using var cleanup = new NpgsqlConnection(_fixture.ConnectionString);
        await cleanup.OpenAsync();
        await using var cmd = cleanup.CreateCommand();
        cmd.CommandText = $"DELETE FROM \"{Schema}\".event_dispatch_kv";
        await cmd.ExecuteNonQueryAsync();
        return db;
    }

    [Fact]
    public async Task Get_Missing_ReturnsNull()
    {
        await using var db = await CreateAsync();
        Assert.Null(await db.GetExternalStateAsync("svc", "wf", "k"));
    }

    [Fact]
    public async Task Upsert_Insert_RoundTrips()
    {
        await using var db = await CreateAsync();
        var state = new ExternalState("svc", "wf", "k", "value-1", UpdateTime: 100.5m, UpdateSeq: 7);
        var result = await db.UpsertExternalStateAsync(state);
        Assert.Equal("value-1", result.Value);
        Assert.Equal(100.5m, result.UpdateTime);
        Assert.Equal(7, result.UpdateSeq);

        var fetched = await db.GetExternalStateAsync("svc", "wf", "k");
        Assert.NotNull(fetched);
        Assert.Equal("value-1", fetched!.Value);
        Assert.Equal(100.5m, fetched.UpdateTime);
        Assert.Equal(7, fetched.UpdateSeq);
    }

    [Fact]
    public async Task Upsert_StrictlyNewer_OverwritesValue()
    {
        await using var db = await CreateAsync();
        await db.UpsertExternalStateAsync(new ExternalState("svc", "wf", "k", "old", 100m, 5));
        var result = await db.UpsertExternalStateAsync(new ExternalState("svc", "wf", "k", "new", 200m, 10));
        Assert.Equal("new", result.Value);
        Assert.Equal(200m, result.UpdateTime);
        Assert.Equal(10, result.UpdateSeq);
    }

    [Fact]
    public async Task Upsert_OlderInTimeAndSeq_KeepsExistingValueButAdvancesMaxes()
    {
        await using var db = await CreateAsync();
        await db.UpsertExternalStateAsync(new ExternalState("svc", "wf", "k", "current", 200m, 10));
        var result = await db.UpsertExternalStateAsync(new ExternalState("svc", "wf", "k", "stale", 100m, 5));
        // Value not overwritten (incoming is older), but max(time, seq) still advances trivially since incoming is smaller.
        Assert.Equal("current", result.Value);
        Assert.Equal(200m, result.UpdateTime);
        Assert.Equal(10, result.UpdateSeq);
    }

    [Fact]
    public async Task Upsert_OnNullExisting_OverwritesValue()
    {
        await using var db = await CreateAsync();
        // Insert with null update_time and update_seq.
        await db.UpsertExternalStateAsync(new ExternalState("svc", "wf", "k", "first", null, null));
        // Even with null incoming meta, the existing-null branch should overwrite value.
        var result = await db.UpsertExternalStateAsync(new ExternalState("svc", "wf", "k", "second", null, null));
        Assert.Equal("second", result.Value);
    }
}
