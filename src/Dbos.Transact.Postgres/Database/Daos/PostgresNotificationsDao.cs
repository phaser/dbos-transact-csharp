using System.Data.Common;
using Dbos.Transact.Database.Daos;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Postgres.Database.Daos;

/// <summary>
/// PostgreSQL-backed implementation of <see cref="NotificationsDao"/>.
/// Uses <c>LISTEN/NOTIFY</c> for push notifications. Full implementation follows in DBOS-20.
/// </summary>
public sealed class PostgresNotificationsDao : NotificationsDao
{
    private readonly Func<DbConnection> _connectionFactory;
    private readonly string _schemaPrefix;

    public PostgresNotificationsDao(Func<DbConnection> connectionFactory, string schema)
    {
        _connectionFactory = connectionFactory;
        _schemaPrefix = string.IsNullOrEmpty(schema) ? string.Empty : $"\"{schema}\".";
    }

    public override Task SendAsync(string workflowId, int stepId, string destinationId, object? message, string? topic, string? messageId, string? serialization, CancellationToken ct = default) =>
        throw new NotImplementedException("DBOS-20");

    public override Task SendDirectAsync(string destinationId, object? message, string? topic, string? messageId, string? serialization, CancellationToken ct = default) =>
        throw new NotImplementedException("DBOS-20");

    public override Task<object?> RecvAsync(string workflowId, int stepId, int timeoutStepId, string? topic, TimeSpan? timeout, CancellationToken ct = default) =>
        throw new NotImplementedException("DBOS-20");

    public override Task SetEventAsync(string workflowId, int functionId, string key, object? message, bool asStep, string? serialization, CancellationToken ct = default) =>
        throw new NotImplementedException("DBOS-20");

    public override Task<object?> GetEventAsync(string targetId, string key, TimeSpan? timeout, CancellationToken ct = default) =>
        throw new NotImplementedException("DBOS-20");

    public override Task<NotificationInfo?> GetNotificationInfoAsync(string workflowId, string key, CancellationToken ct = default) =>
        throw new NotImplementedException("DBOS-20");
}
