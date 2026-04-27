using System.Data.Common;
using Dbos.Transact.Database;
using Dbos.Transact.Database.Daos;
using Dbos.Transact.Json;
using Dbos.Transact.Postgres.Database.Daos;
using Npgsql;

namespace Dbos.Transact.Postgres.Database;

/// <summary>
/// PostgreSQL-backed <see cref="SystemDatabase"/> implementation.
/// Creates <c>NpgsqlDataSource</c> from a connection string and wires all DAOs to it.
/// </summary>
public sealed class PostgresSystemDatabase : SystemDatabase
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly bool _ownsDataSource;

    protected override WorkflowDao WorkflowDao { get; }
    protected override StepsDao StepsDao { get; }
    protected override QueuesDao QueuesDao { get; }
    protected override NotificationsDao NotificationsDao { get; }
    protected override SchedulesDao SchedulesDao { get; }
    protected override StreamsDao StreamsDao { get; }

    public PostgresSystemDatabase(string connectionString, string schema = Constants.DbSchema, IDbosSerializer? serializer = null)
        : this(NpgsqlDataSource.Create(connectionString), schema, ownsDataSource: true, serializer) { }

    public PostgresSystemDatabase(NpgsqlDataSource dataSource, string schema = Constants.DbSchema, bool ownsDataSource = false, IDbosSerializer? serializer = null)
    {
        _dataSource = dataSource;
        _ownsDataSource = ownsDataSource;
        var resolvedSerializer = serializer ?? DbosJsonSerializer.Instance;

        DbConnection Factory() => _dataSource.CreateConnection();

        WorkflowDao = new PostgresWorkflowDao(Factory, schema);
        StepsDao = new PostgresStepsDao(Factory, schema);
        QueuesDao = new PostgresQueuesDao(Factory, schema);
        NotificationsDao = new PostgresNotificationsDao(Factory, schema);
        SchedulesDao = new PostgresSchedulesDao(Factory, schema, resolvedSerializer);
        StreamsDao = new PostgresStreamsDao(Factory, schema);
    }

    protected override async Task<DbConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var connection = _dataSource.CreateConnection();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        return connection;
    }

    public override Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;

    public override async ValueTask DisposeAsync()
    {
        if (_ownsDataSource)
            await _dataSource.DisposeAsync();
    }

    protected override bool IsRetryable(Exception exception)
    {
        if (exception is NpgsqlException npgsql)
        {
            var state = npgsql.SqlState;
            if (state is null) return false;
            // Connection failures (class 08) and serialization failures (40xxx) / too many connections (53300)
            return state.StartsWith("08", StringComparison.Ordinal)
                || state.StartsWith("40", StringComparison.Ordinal)
                || state == "53300";
        }
        return false;
    }
}
