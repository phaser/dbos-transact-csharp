using Dbos.Transact.Migrations;
using Dbos.Transact.Postgres.Database;
using Dbos.Transact.Tests.Fixtures;
using Npgsql;

namespace Dbos.Transact.Tests.Database;

[Collection("Postgres")]
public sealed class PostgresAdvisoryLockTests : IClassFixture<PostgresFixture>
{
    private const string Schema = "dbos_lock_pg_test";

    private readonly PostgresFixture _fixture;

    public PostgresAdvisoryLockTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task EnsureMigratedAsync()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Postgres, Schema);
        await mgr.RunAsync();
    }

    [Fact]
    public async Task FirstAcquireSucceeds_SecondReturnsNull_ReleasedThenSecondSucceeds()
    {
        await EnsureMigratedAsync();
        await using var dbA = new PostgresSystemDatabase(_fixture.ConnectionString, Schema);
        await using var dbB = new PostgresSystemDatabase(_fixture.ConnectionString, Schema);

        const string key = "scheduler-leader-test-key";

        var holderA = await dbA.TryAcquireSchedulerLeaderLockAsync(key);
        Assert.NotNull(holderA);

        var holderB = await dbB.TryAcquireSchedulerLeaderLockAsync(key);
        Assert.Null(holderB);

        await holderA!.DisposeAsync();

        var holderB2 = await dbB.TryAcquireSchedulerLeaderLockAsync(key);
        Assert.NotNull(holderB2);
        await holderB2!.DisposeAsync();
    }

    [Fact]
    public async Task DifferentKeys_BothAcquired()
    {
        await EnsureMigratedAsync();
        await using var dbA = new PostgresSystemDatabase(_fixture.ConnectionString, Schema);

        var keyA = "leader-key-A-" + Guid.NewGuid();
        var keyB = "leader-key-B-" + Guid.NewGuid();

        var holderA = await dbA.TryAcquireSchedulerLeaderLockAsync(keyA);
        var holderB = await dbA.TryAcquireSchedulerLeaderLockAsync(keyB);

        Assert.NotNull(holderA);
        Assert.NotNull(holderB);

        await holderA!.DisposeAsync();
        await holderB!.DisposeAsync();
    }
}
