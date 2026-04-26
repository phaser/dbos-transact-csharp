using Dbos.Transact.Workflow;

namespace Dbos.Transact.Database.Daos;

/// <summary>
/// Data-access methods for DBOS notifications (send/recv) and events (setEvent/getEvent).
/// Port of Java's <c>NotificationsDAO</c>. Dialect-specific SQL is provided by subclasses.
/// </summary>
public abstract class NotificationsDao
{
    public abstract Task SendAsync(
        string workflowId,
        int stepId,
        string destinationId,
        object? message,
        string? topic,
        string? messageId,
        string? serialization,
        CancellationToken ct = default);

    public abstract Task SendDirectAsync(
        string destinationId,
        object? message,
        string? topic,
        string? messageId,
        string? serialization,
        CancellationToken ct = default);

    public abstract Task<object?> RecvAsync(
        string workflowId,
        int stepId,
        int timeoutStepId,
        string? topic,
        TimeSpan? timeout,
        CancellationToken ct = default);

    public abstract Task SetEventAsync(
        string workflowId,
        int functionId,
        string key,
        object? message,
        bool asStep,
        string? serialization,
        CancellationToken ct = default);

    public abstract Task<object?> GetEventAsync(
        string targetId,
        string key,
        TimeSpan? timeout,
        CancellationToken ct = default);

    public abstract Task<NotificationInfo?> GetNotificationInfoAsync(
        string workflowId,
        string key,
        CancellationToken ct = default);
}
