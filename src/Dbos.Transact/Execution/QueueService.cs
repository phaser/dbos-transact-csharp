using Dbos.Transact.Database;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Execution;

/// <summary>
/// Background service that dequeues ENQUEUED workflows and hands them to <see cref="DbosExecutor"/>
/// for execution. One polling loop per queue; adaptive interval with exponential back-off on error.
/// Port of Java's <c>QueueService</c>.
/// </summary>
public sealed class QueueService : IAsyncDisposable
{
    private readonly SystemDatabase _db;
    private readonly DbosExecutor _executor;
    private readonly List<Task> _pollerTasks = new();
    private readonly CancellationTokenSource _cts = new();
    private volatile bool _paused;
    private double _speedup = 1.0;

    private static readonly TimeSpan MinPollingInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxPollingInterval = TimeSpan.FromSeconds(120);

    public QueueService(DbosExecutor executor, SystemDatabase db)
    {
        _executor = executor;
        _db = db;
    }

    public void SetSpeedupForTest() => _speedup = 0.01;

    public void Pause() => _paused = true;

    public void Unpause() => _paused = false;

    public bool IsStopped => _cts.IsCancellationRequested;

    /// <summary>
    /// Starts background poller tasks for each queue in <paramref name="queues"/> that is
    /// also in <paramref name="listenQueues"/> (or if <paramref name="listenQueues"/> is empty,
    /// for all queues). Also starts the delayed-workflow transition timer.
    /// Port of Java's <c>start</c>.
    /// </summary>
    public void Start(IReadOnlyCollection<Queue> queues, IReadOnlySet<string> listenQueues)
    {
        var ct = _cts.Token;

        // Transition DELAYED → ENQUEUED every second.
        _pollerTasks.Add(Task.Run(() => RunDelayedTransitionLoopAsync(ct), ct));

        foreach (var queue in queues)
        {
            var listening = listenQueues.Count == 0 || listenQueues.Contains(queue.Name);
            if (!listening)
                continue;

            var capturedQueue = queue;
            _pollerTasks.Add(Task.Run(() => RunQueuePollerAsync(capturedQueue, ct), ct));
        }
    }

    private async Task RunDelayedTransitionLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (!_paused)
            {
                try { await _db.TransitionDelayedWorkflowsAsync(ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
                catch (Exception) { /* best-effort */ }
            }

            try { await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task RunQueuePollerAsync(Queue queue, CancellationToken ct)
    {
        var interval = MinPollingInterval;

        while (!ct.IsCancellationRequested)
        {
            if (!_paused)
            {
                try
                {
                    if (queue.PartitioningEnabled)
                    {
                        var partitions = await _db.GetQueuePartitionsAsync(queue.Name, ct).ConfigureAwait(false);
                        foreach (var partition in partitions)
                            await ProcessPartitionAsync(queue, partition, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        await ProcessPartitionAsync(queue, null, ct).ConfigureAwait(false);
                    }

                    // Shrink interval on success (down to minimum).
                    interval = interval < MinPollingInterval ? MinPollingInterval
                        : TimeSpan.FromMilliseconds(interval.TotalMilliseconds * 0.9);
                    if (interval < MinPollingInterval) interval = MinPollingInterval;
                }
                catch (OperationCanceledException) { return; }
                catch (Exception)
                {
                    // Double interval on error (up to maximum).
                    interval = TimeSpan.FromMilliseconds(interval.TotalMilliseconds * 2);
                    if (interval > MaxPollingInterval) interval = MaxPollingInterval;
                }
            }

            var jitter = 0.95 + Random.Shared.NextDouble() * 0.1;
            var delayMs = (long)(jitter * interval.TotalMilliseconds * _speedup);
            try { await Task.Delay(TimeSpan.FromMilliseconds(delayMs), ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task ProcessPartitionAsync(Queue queue, string? partition, CancellationToken ct)
    {
        var workflowIds = await _db.GetAndStartQueuedWorkflowsAsync(
            queue, _executor.ExecutorId, appVersion: null, partition, ct).ConfigureAwait(false);

        foreach (var id in workflowIds)
        {
            try
            {
                await _executor.ExecuteWorkflowByIdAsync(
                    id, isRecoveryRequest: false, isDequeuedRequest: true, ct).ConfigureAwait(false);
            }
            catch (Exception) { /* logged by executor; continue processing remaining ids */ }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        try { await Task.WhenAll(_pollerTasks).ConfigureAwait(false); }
        catch { /* ignore cancellation exceptions on shutdown */ }
        _cts.Dispose();
    }
}
