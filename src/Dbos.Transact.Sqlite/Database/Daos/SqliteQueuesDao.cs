using System.Data.Common;
using Dbos.Transact.Database.Daos;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Sqlite.Database.Daos;

/// <summary>SQLite-backed implementation of <see cref="QueuesDao"/>. Full SQL in DBOS-20.</summary>
public sealed class SqliteQueuesDao : QueuesDao
{
    private readonly Func<DbConnection> _connectionFactory;
    public SqliteQueuesDao(Func<DbConnection> connectionFactory) { _connectionFactory = connectionFactory; }

    public override Task<bool> ClearQueueAssignmentAsync(string workflowId, CancellationToken ct = default) => throw new NotImplementedException("DBOS-20");
    public override Task<IReadOnlyList<string>> GetQueuePartitionsAsync(string queueName, CancellationToken ct = default) => throw new NotImplementedException("DBOS-20");
    public override Task<IReadOnlyList<string>> GetAndStartQueuedWorkflowsAsync(Queue queue, string executorId, string? appVersion, string? partitionKey, CancellationToken ct = default) => throw new NotImplementedException("DBOS-20");
}
