using System.Data.Common;
using Dapper;
using Dbos.Transact.Database;
using Dbos.Transact.Database.Daos;

namespace Dbos.Transact.Postgres.Database.Daos;

/// <summary>PostgreSQL-backed implementation of <see cref="EventDispatchKvDao"/>.</summary>
public sealed class PostgresEventDispatchKvDao : EventDispatchKvDao
{
    private readonly Func<DbConnection> _connectionFactory;
    private readonly string _schemaPrefix;

    public PostgresEventDispatchKvDao(Func<DbConnection> connectionFactory, string schema)
    {
        _connectionFactory = connectionFactory;
        _schemaPrefix = string.IsNullOrEmpty(schema) ? string.Empty : $"\"{schema}\".";
    }

    private sealed class Row
    {
        public string? Value { get; set; }
        public decimal? UpdateTime { get; set; }
        public long? UpdateSeq { get; set; }
    }

    public override async Task<ExternalState?> GetExternalStateAsync(string service, string workflowName, string key, CancellationToken ct = default)
    {
        var sql = $"""
            SELECT value AS Value, update_time AS UpdateTime, update_seq AS UpdateSeq
            FROM {_schemaPrefix}event_dispatch_kv
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
        var sql = $"""
            INSERT INTO {_schemaPrefix}event_dispatch_kv
                (service_name, workflow_fn_name, key, value, update_time, update_seq)
            VALUES (@Service, @WorkflowName, @Key, @Value, @UpdateTime, @UpdateSeq)
            ON CONFLICT (service_name, workflow_fn_name, key) DO UPDATE SET
                update_time = GREATEST(EXCLUDED.update_time, event_dispatch_kv.update_time),
                update_seq  = GREATEST(EXCLUDED.update_seq,  event_dispatch_kv.update_seq),
                value = CASE WHEN (
                    EXCLUDED.update_time > event_dispatch_kv.update_time
                    OR EXCLUDED.update_seq > event_dispatch_kv.update_seq
                    OR (event_dispatch_kv.update_time IS NULL AND event_dispatch_kv.update_seq IS NULL)
                ) THEN EXCLUDED.value ELSE event_dispatch_kv.value END
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
