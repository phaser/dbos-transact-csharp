using System.Data.Common;
using Dbos.Transact.Database.Daos;

namespace Dbos.Transact.Sqlite.Database.Daos;

/// <summary>SQLite-backed implementation of <see cref="StreamsDao"/>. Full SQL in DBOS-20.</summary>
public sealed class SqliteStreamsDao : StreamsDao
{
    private readonly Func<DbConnection> _connectionFactory;
    public SqliteStreamsDao(Func<DbConnection> connectionFactory) { _connectionFactory = connectionFactory; }

    public override Task WriteStreamFromStepAsync(string workflowId, int functionId, string key, object? value, string? serializationFormat, CancellationToken ct = default) => throw new NotImplementedException("DBOS-20");
    public override Task WriteStreamFromWorkflowAsync(string workflowId, int functionId, string key, object? value, string? serializationFormat, CancellationToken ct = default) => throw new NotImplementedException("DBOS-20");
    public override Task CloseStreamAsync(string workflowId, int functionId, string key, CancellationToken ct = default) => throw new NotImplementedException("DBOS-20");
    public override Task<object?> ReadStreamAsync(string workflowId, string key, int offset, CancellationToken ct = default) => throw new NotImplementedException("DBOS-20");
    public override Task<IReadOnlyDictionary<string, IReadOnlyList<object?>>> GetAllStreamEntriesAsync(string workflowId, CancellationToken ct = default) => throw new NotImplementedException("DBOS-20");
}
