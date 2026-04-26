using System.Data.Common;
using Dbos.Transact.Database.Daos;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Sqlite.Database.Daos;

/// <summary>SQLite-backed implementation of <see cref="NotificationsDao"/>. Uses polling. Full SQL in DBOS-20.</summary>
public sealed class SqliteNotificationsDao : NotificationsDao
{
    private readonly Func<DbConnection> _connectionFactory;
    public SqliteNotificationsDao(Func<DbConnection> connectionFactory) { _connectionFactory = connectionFactory; }

    public override Task SendAsync(string workflowId, int stepId, string destinationId, object? message, string? topic, string? messageId, string? serialization, CancellationToken ct = default) => throw new NotImplementedException("DBOS-20");
    public override Task SendDirectAsync(string destinationId, object? message, string? topic, string? messageId, string? serialization, CancellationToken ct = default) => throw new NotImplementedException("DBOS-20");
    public override Task<object?> RecvAsync(string workflowId, int stepId, int timeoutStepId, string? topic, TimeSpan? timeout, CancellationToken ct = default) => throw new NotImplementedException("DBOS-20");
    public override Task SetEventAsync(string workflowId, int functionId, string key, object? message, bool asStep, string? serialization, CancellationToken ct = default) => throw new NotImplementedException("DBOS-20");
    public override Task<object?> GetEventAsync(string targetId, string key, TimeSpan? timeout, CancellationToken ct = default) => throw new NotImplementedException("DBOS-20");
    public override Task<NotificationInfo?> GetNotificationInfoAsync(string workflowId, string key, CancellationToken ct = default) => throw new NotImplementedException("DBOS-20");
}
