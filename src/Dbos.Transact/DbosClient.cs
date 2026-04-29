using Dbos.Transact.Database;
using Dbos.Transact.Json;
using Dbos.Transact.Workflow;
using Dbos.Transact.Workflow.Internal;

namespace Dbos.Transact;

/// <summary>
/// Standalone client for interacting with a DBOS application via direct system database access,
/// without launching a local executor. Useful for monitoring, lifecycle management, and external
/// enqueue. Port of Java's <c>DBOSClient</c>.
/// </summary>
public sealed class DbosClient : IAsyncDisposable
{
    private readonly SystemDatabase _systemDatabase;
    private readonly bool _ownsSystemDatabase;
    private readonly IDbosSerializer _serializer;
    private bool _disposed;

    /// <summary>
    /// Wraps an existing <see cref="SystemDatabase"/>. By default the client does not take ownership;
    /// pass <paramref name="ownsSystemDatabase"/> = true to have <see cref="DisposeAsync"/> dispose it too.
    /// </summary>
    public DbosClient(
        SystemDatabase systemDatabase,
        bool ownsSystemDatabase = false,
        IDbosSerializer? serializer = null)
    {
        ArgumentNullException.ThrowIfNull(systemDatabase);
        _systemDatabase = systemDatabase;
        _ownsSystemDatabase = ownsSystemDatabase;
        _serializer = serializer ?? DbosJsonSerializer.Instance;
    }

    public IDbosSerializer Serializer => _serializer;

    // ── Workflow status / handles ─────────────────────────────────────────────

    /// <summary>Returns a handle for a workflow ID. The workflow may or may not exist.</summary>
    public WorkflowHandle<T> RetrieveWorkflow<T>(string workflowId)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowId);
        ThrowIfDisposed();
        return new WorkflowHandleDbPoll<T>(_systemDatabase, _serializer, workflowId);
    }

    /// <summary>Returns the status of a workflow, or <c>null</c> if it does not exist.</summary>
    public Task<WorkflowStatus?> GetWorkflowStatusAsync(string workflowId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowId);
        ThrowIfDisposed();
        return _systemDatabase.GetWorkflowStatusAsync(workflowId, ct);
    }

    /// <summary>Polls until the workflow completes, then returns its result.</summary>
    public Task<T> GetResultAsync<T>(string workflowId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowId);
        ThrowIfDisposed();
        return _systemDatabase.AwaitWorkflowResultAsync<T>(workflowId, _serializer, ct);
    }

    public Task<IReadOnlyList<WorkflowStatus>> ListWorkflowsAsync(
        ListWorkflowsInput? input = null, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return _systemDatabase.ListWorkflowsAsync(input ?? new ListWorkflowsInput(), ct);
    }

    public Task<IReadOnlyList<StepInfo>> ListWorkflowStepsAsync(
        string workflowId, int? limit = null, int? offset = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowId);
        ThrowIfDisposed();
        return _systemDatabase.ListWorkflowStepsAsync(workflowId, loadOutput: true, limit, offset, ct);
    }

    // ── Workflow management ───────────────────────────────────────────────────

    public Task CancelWorkflowAsync(string workflowId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowId);
        ThrowIfDisposed();
        return _systemDatabase.CancelWorkflowsAsync([workflowId], ct);
    }

    public Task CancelWorkflowsAsync(IReadOnlyList<string> workflowIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workflowIds);
        ThrowIfDisposed();
        return _systemDatabase.CancelWorkflowsAsync(workflowIds, ct);
    }

    public async Task<WorkflowHandle<T>> ResumeWorkflowAsync<T>(
        string workflowId, string? queueName = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowId);
        ThrowIfDisposed();
        await _systemDatabase.ResumeWorkflowsAsync([workflowId], queueName, ct).ConfigureAwait(false);
        return RetrieveWorkflow<T>(workflowId);
    }

    public Task DeleteWorkflowAsync(string workflowId, bool deleteChildren = false, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowId);
        ThrowIfDisposed();
        return _systemDatabase.DeleteWorkflowsAsync([workflowId], deleteChildren, ct);
    }

    public Task DeleteWorkflowsAsync(
        IReadOnlyList<string> workflowIds, bool deleteChildren = false, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workflowIds);
        ThrowIfDisposed();
        return _systemDatabase.DeleteWorkflowsAsync(workflowIds, deleteChildren, ct);
    }

    public async Task<WorkflowHandle<T>> ForkWorkflowAsync<T>(
        string workflowId, int startStep, ForkOptions? options = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowId);
        ThrowIfDisposed();
        var forkedId = await _systemDatabase
            .ForkWorkflowAsync(workflowId, startStep, options ?? new ForkOptions(), ct)
            .ConfigureAwait(false);
        return RetrieveWorkflow<T>(forkedId);
    }

    // ── Notifications / events ────────────────────────────────────────────────

    /// <summary>Sends a message to a workflow from outside any workflow context.</summary>
    public Task SendAsync(
        string destinationId,
        object? message,
        string? topic = null,
        string? idempotencyKey = null,
        string? serializationFormat = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(destinationId);
        ThrowIfDisposed();
        return _systemDatabase.SendDirectAsync(destinationId, message, topic, idempotencyKey, serializationFormat, ct);
    }

    /// <summary>Reads an event published by a workflow, blocking up to <paramref name="timeout"/>.</summary>
    public Task<object?> GetEventAsync(
        string targetWorkflowId, string key, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(key);
        ThrowIfDisposed();
        return _systemDatabase.GetEventAsync(targetWorkflowId, key, timeout, ct);
    }

    // ── Schedules ─────────────────────────────────────────────────────────────

    public Task CreateScheduleAsync(WorkflowSchedule schedule, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ThrowIfDisposed();
        return _systemDatabase.CreateScheduleAsync(schedule, ct);
    }

    public Task<WorkflowSchedule?> GetScheduleAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ThrowIfDisposed();
        return _systemDatabase.GetScheduleAsync(name, ct);
    }

    public Task<IReadOnlyList<WorkflowSchedule>> ListSchedulesAsync(
        IReadOnlyList<ScheduleStatus>? statuses = null,
        IReadOnlyList<string>? workflowNames = null,
        IReadOnlyList<string>? namePrefixes = null,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return _systemDatabase.ListSchedulesAsync(statuses, workflowNames, namePrefixes, ct);
    }

    public Task DeleteScheduleAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ThrowIfDisposed();
        return _systemDatabase.DeleteScheduleAsync(name, ct);
    }

    public Task PauseScheduleAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ThrowIfDisposed();
        return _systemDatabase.PauseScheduleAsync(name, ct);
    }

    public Task ResumeScheduleAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ThrowIfDisposed();
        return _systemDatabase.ResumeScheduleAsync(name, ct);
    }

    public Task ApplySchedulesAsync(IReadOnlyList<WorkflowSchedule> schedules, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(schedules);
        ThrowIfDisposed();
        return _systemDatabase.ApplySchedulesAsync(schedules, ct);
    }

    // ── External state ────────────────────────────────────────────────────────

    public Task<ExternalState?> GetExternalStateAsync(
        string service, string workflowName, string key, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return _systemDatabase.GetExternalStateAsync(service, workflowName, key, ct);
    }

    public Task<ExternalState> UpsertExternalStateAsync(ExternalState state, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ThrowIfDisposed();
        return _systemDatabase.UpsertExternalStateAsync(state, ct);
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ownsSystemDatabase)
            await _systemDatabase.DisposeAsync().ConfigureAwait(false);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
