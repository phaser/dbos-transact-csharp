using System.Data.Common;
using Dapper;
using Dbos.Transact.Database;
using Dbos.Transact.Database.Daos;

namespace Dbos.Transact.Sqlite.Database.Daos;

/// <summary>SQLite-backed implementation of <see cref="EventDispatchKvDao"/>.</summary>
public sealed class SqliteEventDispatchKvDao : EventDispatchKvDao
{
    private readonly Func<DbConnection> _connectionFactory;

    public SqliteEventDispatchKvDao(Func<DbConnection> connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private sealed class Row
    {
        public string? Value { get; set; }
        public decimal? UpdateTime { get; set; }
        public long? UpdateSeq { get; set; }
    }

    public override async Task<ExternalState?> GetExternalStateAsync(string service, string workflowName, string key, CancellationToken ct = default)
    {
        const string sql = """
            SELECT value AS Value, update_time AS UpdateTime, update_seq AS UpdateSeq
            FROM event_dispatch_kv
            WHERE service_name = @Service AND workflow_fn_name = @WorkflowName AND key = @Key
            """;
        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(
            new CommandDefinition(sql, new { Service = service, WorkflowName = workflowName, Key = key }, cancellationToken: ct)).ConfigureAwait(false);
        return row is null ? null : new ExternalState(service, workflowName, key, row.Value, row.UpdateTime, row.UpdateSeq);
    }

    public override async Task<ExternalState> UpsertExternalStateAsync(ExternalState state, CancellationToken ct = default)
    {
        // SQLite: MAX(a, b) as a scalar function (3.7+), ON CONFLICT … DO UPDATE (3.24+).
        const string sql = """
            INSERT INTO event_dispatch_kv
                (service_name, workflow_fn_name, key, value, update_time, update_seq)
            VALUES (@Service, @WorkflowName, @Key, @Value, @UpdateTime, @UpdateSeq)
            ON CONFLICT(service_name, workflow_fn_name, key) DO UPDATE SET
                update_time = MAX(excluded.update_time, event_dispatch_kv.update_time),
                update_seq  = MAX(excluded.update_seq,  event_dispatch_kv.update_seq),
                value = CASE WHEN (
                    excluded.update_time > event_dispatch_kv.update_time
                    OR excluded.update_seq > event_dispatch_kv.update_seq
                    OR (event_dispatch_kv.update_time IS NULL AND event_dispatch_kv.update_seq IS NULL)
                ) THEN excluded.value ELSE event_dispatch_kv.value END
            RETURNING value AS Value, update_time AS UpdateTime, update_seq AS UpdateSeq
            """;
        await using var connection = _connectionFactory();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(
            new CommandDefinition(sql, new
            {
                Service = state.Service,
                WorkflowName = state.WorkflowName,
                Key = state.Key,
                Value = state.Value,
                UpdateTime = state.UpdateTime,
                UpdateSeq = state.UpdateSeq,
            }, cancellationToken: ct)).ConfigureAwait(false);

        return row is null
            ? throw new InvalidOperationException(
                $"UpsertExternalState returned no row for {state.Service}/{state.WorkflowName}/{state.Key}")
            : new ExternalState(state.Service, state.WorkflowName, state.Key, row.Value, row.UpdateTime, row.UpdateSeq);
    }
}
