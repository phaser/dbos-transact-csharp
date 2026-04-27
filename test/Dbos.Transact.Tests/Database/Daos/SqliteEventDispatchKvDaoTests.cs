using Dbos.Transact.Database;
using Dbos.Transact.Migrations;
using Dbos.Transact.Sqlite.Database;
using Dbos.Transact.Tests.Fixtures;
using Microsoft.Data.Sqlite;

namespace Dbos.Transact.Tests.Database.Daos;

public sealed class SqliteEventDispatchKvDaoTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteFixture _fixture = new(SqliteFixture.Mode.File);

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();
    public void Dispose() { _fixture.Dispose(); GC.SuppressFinalize(this); }

    private async Task<SystemDatabase> CreateAsync()
    {
        await using var conn = new SqliteConnection(_fixture.ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Sqlite);
        await mgr.RunAsync();
        return new SqliteSystemDatabase(_fixture.ConnectionString);
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
        Assert.Equal("current", result.Value);
        Assert.Equal(200m, result.UpdateTime);
        Assert.Equal(10, result.UpdateSeq);
    }

    [Fact]
    public async Task Upsert_OnNullExisting_OverwritesValue()
    {
        await using var db = await CreateAsync();
        await db.UpsertExternalStateAsync(new ExternalState("svc", "wf", "k", "first", null, null));
        var result = await db.UpsertExternalStateAsync(new ExternalState("svc", "wf", "k", "second", null, null));
        Assert.Equal("second", result.Value);
    }
}
