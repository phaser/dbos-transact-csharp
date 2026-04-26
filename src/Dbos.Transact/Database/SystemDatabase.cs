using Dbos.Transact.Database.Daos;
using Dbos.Transact.Workflow;
using Dbos.Transact.Workflow.Internal;

namespace Dbos.Transact.Database;

/// <summary>
/// Abstract base for DBOS system-table access. Handles connection lifecycle, retry
/// logic, and public API orchestration. Dialect-specific SQL lives in
/// <c>PostgresSystemDatabase</c> and <c>SqliteSystemDatabase</c>.
/// Port of Java's <c>SystemDatabase</c>.
/// </summary>
public abstract class SystemDatabase : IAsyncDisposable
{
    private const int MaxRetries = 3;
    private const long BaseRetryDelayMs = 100;

    protected abstract WorkflowDao WorkflowDao { get; }
    protected abstract StepsDao StepsDao { get; }
    protected abstract QueuesDao QueuesDao { get; }
    protected abstract NotificationsDao NotificationsDao { get; }
    protected abstract SchedulesDao SchedulesDao { get; }
    protected abstract StreamsDao StreamsDao { get; }

    public abstract Task StartAsync(CancellationToken ct = default);
    public abstract ValueTask DisposeAsync();

    // ── Workflow ──────────────────────────────────────────────────────────

    public Task<WorkflowInitResult> InitWorkflowStatusAsync(
        WorkflowStatusInternal initStatus,
        int maxRetries,
        bool isRecoveryRequest,
        bool isDequeuedRequest,
        CancellationToken ct = default) =>
        DbRetryAsync(
            c => WorkflowDao.InitWorkflowStatusAsync(initStatus, maxRetries, isRecoveryRequest, isDequeuedRequest, c),
            ct);

    public Task RecordWorkflowOutputAsync(string workflowId, string? result, CancellationToken ct = default) =>
        DbRetryAsync(c => WorkflowDao.RecordWorkflowOutputAsync(workflowId, result, c), ct);

    public Task RecordWorkflowErrorAsync(string workflowId, string? errorPayload, CancellationToken ct = default) =>
        DbRetryAsync(c => WorkflowDao.RecordWorkflowErrorAsync(workflowId, errorPayload, c), ct);

    public Task<WorkflowStatus?> GetWorkflowStatusAsync(string workflowId, CancellationToken ct = default) =>
        DbRetryAsync(c => WorkflowDao.GetWorkflowStatusAsync(workflowId, c), ct);

    public Task<string?> GetWorkflowSerializationAsync(string workflowId, CancellationToken ct = default) =>
        DbRetryAsync(c => WorkflowDao.GetWorkflowSerializationAsync(workflowId, c), ct);

    public Task<IReadOnlyList<WorkflowStatus>> ListWorkflowsAsync(ListWorkflowsInput input, CancellationToken ct = default) =>
        DbRetryAsync(c => WorkflowDao.ListWorkflowsAsync(input, c), ct);

    public Task<IReadOnlyList<WorkflowAggregateRow>> GetWorkflowAggregatesAsync(GetWorkflowAggregatesInput input, CancellationToken ct = default) =>
        DbRetryAsync(c => WorkflowDao.GetWorkflowAggregatesAsync(input, c), ct);

    public Task<IReadOnlyList<WorkflowStatus>> GetPendingWorkflowsAsync(IReadOnlyList<string> executorIds, string? appVersion, CancellationToken ct = default) =>
        DbRetryAsync(c => WorkflowDao.GetPendingWorkflowsAsync(executorIds, appVersion, c), ct);

    public Task<T> AwaitWorkflowResultAsync<T>(string workflowId, CancellationToken ct = default) =>
        DbRetryAsync(c => WorkflowDao.AwaitWorkflowResultAsync<T>(workflowId, c), ct);

    public Task CancelWorkflowsAsync(IReadOnlyList<string> workflowIds, CancellationToken ct = default) =>
        DbRetryAsync(c => WorkflowDao.CancelWorkflowsAsync(workflowIds, c), ct);

    public Task ResumeWorkflowsAsync(IReadOnlyList<string> workflowIds, string? queueName, CancellationToken ct = default) =>
        DbRetryAsync(c => WorkflowDao.ResumeWorkflowsAsync(workflowIds, queueName, c), ct);

    public Task DeleteWorkflowsAsync(IReadOnlyList<string> workflowIds, bool deleteChildren, CancellationToken ct = default) =>
        DbRetryAsync(c => WorkflowDao.DeleteWorkflowsAsync(workflowIds, deleteChildren, c), ct);

    public Task<string> ForkWorkflowAsync(string originalWorkflowId, int startStep, ForkOptions options, CancellationToken ct = default) =>
        DbRetryAsync(c => WorkflowDao.ForkWorkflowAsync(originalWorkflowId, startStep, options, c), ct);

    // ── Steps ─────────────────────────────────────────────────────────────

    public Task<IReadOnlyList<StepInfo>> ListWorkflowStepsAsync(
        string workflowId, bool loadOutput, int? limit, int? offset, CancellationToken ct = default) =>
        DbRetryAsync(c => StepsDao.ListWorkflowStepsAsync(workflowId, loadOutput, limit, offset, c), ct);

    public Task SleepAsync(string workflowId, int functionId, TimeSpan duration, CancellationToken ct = default) =>
        DbRetryAsync(c => StepsDao.SleepAsync(workflowId, functionId, duration, c), ct);

    // ── Queues ────────────────────────────────────────────────────────────

    public Task<bool> ClearQueueAssignmentAsync(string workflowId, CancellationToken ct = default) =>
        DbRetryAsync(c => QueuesDao.ClearQueueAssignmentAsync(workflowId, c), ct);

    public Task<IReadOnlyList<string>> GetQueuePartitionsAsync(string queueName, CancellationToken ct = default) =>
        DbRetryAsync(c => QueuesDao.GetQueuePartitionsAsync(queueName, c), ct);

    public Task<IReadOnlyList<string>> GetAndStartQueuedWorkflowsAsync(
        Queue queue, string executorId, string? appVersion, string? partitionKey, CancellationToken ct = default) =>
        DbRetryAsync(c => QueuesDao.GetAndStartQueuedWorkflowsAsync(queue, executorId, appVersion, partitionKey, c), ct);

    // ── Notifications / Events ────────────────────────────────────────────

    public Task SendAsync(
        string workflowId, int stepId, string destinationId, object? message,
        string? topic, string? messageId, string? serialization, CancellationToken ct = default) =>
        DbRetryAsync(c => NotificationsDao.SendAsync(workflowId, stepId, destinationId, message, topic, messageId, serialization, c), ct);

    public Task SendDirectAsync(
        string destinationId, object? message, string? topic, string? messageId, string? serialization, CancellationToken ct = default) =>
        DbRetryAsync(c => NotificationsDao.SendDirectAsync(destinationId, message, topic, messageId, serialization, c), ct);

    public Task<object?> RecvAsync(
        string workflowId, int stepId, int timeoutStepId, string? topic, TimeSpan? timeout, CancellationToken ct = default) =>
        DbRetryAsync(c => NotificationsDao.RecvAsync(workflowId, stepId, timeoutStepId, topic, timeout, c), ct);

    public Task SetEventAsync(
        string workflowId, int functionId, string key, object? message, bool asStep, string? serialization, CancellationToken ct = default) =>
        DbRetryAsync(c => NotificationsDao.SetEventAsync(workflowId, functionId, key, message, asStep, serialization, c), ct);

    public Task<object?> GetEventAsync(string targetId, string key, TimeSpan? timeout, CancellationToken ct = default) =>
        DbRetryAsync(c => NotificationsDao.GetEventAsync(targetId, key, timeout, c), ct);

    // ── Schedules ─────────────────────────────────────────────────────────

    public Task CreateScheduleAsync(WorkflowSchedule schedule, CancellationToken ct = default) =>
        DbRetryAsync(c => SchedulesDao.CreateScheduleAsync(schedule, c), ct);

    public Task<IReadOnlyList<WorkflowSchedule>> ListSchedulesAsync(
        IReadOnlyList<ScheduleStatus>? statuses = null,
        IReadOnlyList<string>? workflowNames = null,
        IReadOnlyList<string>? scheduleNamePrefixes = null,
        CancellationToken ct = default) =>
        DbRetryAsync(c => SchedulesDao.ListSchedulesAsync(statuses, workflowNames, scheduleNamePrefixes, c), ct);

    public Task<WorkflowSchedule?> GetScheduleAsync(string name, CancellationToken ct = default) =>
        DbRetryAsync(c => SchedulesDao.GetScheduleAsync(name, c), ct);

    public Task PauseScheduleAsync(string name, CancellationToken ct = default) =>
        DbRetryAsync(c => SchedulesDao.PauseScheduleAsync(name, c), ct);

    public Task ResumeScheduleAsync(string name, CancellationToken ct = default) =>
        DbRetryAsync(c => SchedulesDao.ResumeScheduleAsync(name, c), ct);

    public Task UpdateScheduleLastFiredAtAsync(string name, DateTimeOffset lastFiredAt, CancellationToken ct = default) =>
        DbRetryAsync(c => SchedulesDao.UpdateScheduleLastFiredAtAsync(name, lastFiredAt, c), ct);

    public Task DeleteScheduleAsync(string name, CancellationToken ct = default) =>
        DbRetryAsync(c => SchedulesDao.DeleteScheduleAsync(name, c), ct);

    public Task ApplySchedulesAsync(IReadOnlyList<WorkflowSchedule> schedules, CancellationToken ct = default) =>
        DbRetryAsync(c => SchedulesDao.ApplySchedulesAsync(schedules, c), ct);

    // ── Streams ───────────────────────────────────────────────────────────

    public Task WriteStreamFromStepAsync(
        string workflowId, int functionId, string key, object? value, string? serializationFormat, CancellationToken ct = default) =>
        DbRetryAsync(c => StreamsDao.WriteStreamFromStepAsync(workflowId, functionId, key, value, serializationFormat, c), ct);

    public Task WriteStreamFromWorkflowAsync(
        string workflowId, int functionId, string key, object? value, string? serializationFormat, CancellationToken ct = default) =>
        DbRetryAsync(c => StreamsDao.WriteStreamFromWorkflowAsync(workflowId, functionId, key, value, serializationFormat, c), ct);

    public Task CloseStreamAsync(string workflowId, int functionId, string key, CancellationToken ct = default) =>
        DbRetryAsync(c => StreamsDao.CloseStreamAsync(workflowId, functionId, key, c), ct);

    public Task<object?> ReadStreamAsync(string workflowId, string key, int offset, CancellationToken ct = default) =>
        DbRetryAsync(c => StreamsDao.ReadStreamAsync(workflowId, key, offset, c), ct);

    public Task<IReadOnlyDictionary<string, IReadOnlyList<object?>>> GetAllStreamEntriesAsync(string workflowId, CancellationToken ct = default) =>
        DbRetryAsync(c => StreamsDao.GetAllStreamEntriesAsync(workflowId, c), ct);

    // ── Retry infrastructure ──────────────────────────────────────────────

    /// <summary>
    /// Determines whether a database exception is a transient connection or serialization failure
    /// that warrants a retry. Subclasses may override to add dialect-specific codes.
    /// </summary>
    protected virtual bool IsRetryable(Exception exception) => false;

    private async Task<T> DbRetryAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await operation(ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < MaxRetries && IsRetryable(ex))
            {
                long delayMs = (long)(BaseRetryDelayMs * Math.Pow(2, attempt - 1));
                await Task.Delay(TimeSpan.FromMilliseconds(delayMs), ct).ConfigureAwait(false);
            }
        }
    }

    private async Task DbRetryAsync(Func<CancellationToken, Task> operation, CancellationToken ct)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await operation(ct).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (attempt < MaxRetries && IsRetryable(ex))
            {
                long delayMs = (long)(BaseRetryDelayMs * Math.Pow(2, attempt - 1));
                await Task.Delay(TimeSpan.FromMilliseconds(delayMs), ct).ConfigureAwait(false);
            }
        }
    }
}
