using System.Data.Common;
using Dbos.Transact.Database.Daos;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Postgres.Database.Daos;

/// <summary>
/// PostgreSQL-backed implementation of <see cref="QueuesDao"/>.
/// Full SQL implementation follows in DBOS-20.
/// </summary>
public sealed class PostgresQueuesDao : QueuesDao
{
    private readonly Func<DbConnection> _connectionFactory;
    private readonly string _schemaPrefix;

    public PostgresQueuesDao(Func<DbConnection> connectionFactory, string schema)
    {
        _connectionFactory = connectionFactory;
        _schemaPrefix = string.IsNullOrEmpty(schema) ? string.Empty : $"\"{schema}\".";
    }

    public override Task<bool> ClearQueueAssignmentAsync(string workflowId, CancellationToken ct = default) =>
        throw new NotImplementedException("DBOS-20");

    public override Task<IReadOnlyList<string>> GetQueuePartitionsAsync(string queueName, CancellationToken ct = default) =>
        throw new NotImplementedException("DBOS-20");

    public override Task<IReadOnlyList<string>> GetAndStartQueuedWorkflowsAsync(
        Queue queue, string executorId, string? appVersion, string? partitionKey, CancellationToken ct = default) =>
        throw new NotImplementedException("DBOS-20");
}
