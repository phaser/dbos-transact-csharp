using System.Data.Common;
using Dbos.Transact.Database;
using Dbos.Transact.Database.Daos;
using Dbos.Transact.Json;
using Dbos.Transact.Sqlite.Database.Daos;
using Microsoft.Data.Sqlite;

namespace Dbos.Transact.Sqlite.Database;

/// <summary>
/// SQLite-backed <see cref="SystemDatabase"/> implementation.
/// Uses a connection string factory so each operation gets a fresh connection from the pool.
/// </summary>
public sealed class SqliteSystemDatabase : SystemDatabase
{
    private readonly string _connectionString;

    protected override WorkflowDao WorkflowDao { get; }
    protected override StepsDao StepsDao { get; }
    protected override QueuesDao QueuesDao { get; }
    protected override NotificationsDao NotificationsDao { get; }
    protected override SchedulesDao SchedulesDao { get; }
    protected override StreamsDao StreamsDao { get; }
    protected override EventDispatchKvDao EventDispatchKvDao { get; }

    public SqliteSystemDatabase(string connectionString, IDbosSerializer? serializer = null)
    {
        _connectionString = connectionString;
        var resolvedSerializer = serializer ?? DbosJsonSerializer.Instance;

        DbConnection Factory() => new SqliteConnection(_connectionString);

        WorkflowDao = new SqliteWorkflowDao(Factory);
        StepsDao = new SqliteStepsDao(Factory);
        QueuesDao = new SqliteQueuesDao(Factory);
        NotificationsDao = new SqliteNotificationsDao(Factory);
        SchedulesDao = new SqliteSchedulesDao(Factory, resolvedSerializer);
        StreamsDao = new SqliteStreamsDao(Factory);
        EventDispatchKvDao = new SqliteEventDispatchKvDao(Factory);
    }

    protected override async Task<DbConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        return connection;
    }

    public override Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;

    public override ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public override Task<IAsyncDisposable?> TryAcquireSchedulerLeaderLockAsync(string key, CancellationToken ct = default) =>
        // SQLite is single-host: cross-process scheduler leadership is unnecessary.
        // Always grant the lock; the holder is a no-op disposable.
        Task.FromResult<IAsyncDisposable?>(NoOpLockHolder.Instance);

    private sealed class NoOpLockHolder : IAsyncDisposable
    {
        public static readonly NoOpLockHolder Instance = new();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    protected override bool IsRetryable(Exception exception)
    {
        // SQLite error code 5 = SQLITE_BUSY, 6 = SQLITE_LOCKED — transient
        if (exception is SqliteException sqlite)
            return sqlite.SqliteErrorCode is 5 or 6;
        return false;
    }
}
