using System.Collections.Concurrent;
using System.Reflection;
using Cronos;
using Dbos.Transact.Database;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Execution;

/// <summary>
/// Durable cron driver. Polls <c>workflow_schedules</c> and registered <c>[Scheduled]</c>
/// workflows, computes next-fire times via <see cref="CronExpression"/>, and starts the
/// associated workflows at the right moment. Runs leader-only — multiple instances elect
/// a single leader through <see cref="SystemDatabase.TryAcquireSchedulerLeaderLockAsync"/>
/// so each scheduled instant fires once across the cluster.
/// Port of Java's <c>SchedulerService</c>.
/// </summary>
public sealed class SchedulerService : IAsyncDisposable
{
    private const string LeaderLockKey = "dbos-scheduler-leader";
    private static readonly TimeSpan DefaultPollingInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan LeaderRetryInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxJitter = TimeSpan.FromSeconds(10);

    private readonly DbosExecutor _executor;
    private readonly SystemDatabase _db;
    private readonly TimeSpan _pollingInterval;
    private readonly Random _jitterRng = new();
    private readonly ConcurrentDictionary<string, ScheduleRunner> _runners = new();
    private readonly CancellationTokenSource _cts = new();

    private Task? _pollTask;
    private IAsyncDisposable? _leaderLock;
    private volatile bool _paused;

    public SchedulerService(DbosExecutor executor, SystemDatabase db, TimeSpan? pollingInterval = null)
    {
        _executor = executor;
        _db = db;
        _pollingInterval = pollingInterval ?? DefaultPollingInterval;
    }

    public bool IsLeader => _leaderLock is not null;

    public void Pause() => _paused = true;
    public void Unpause() => _paused = false;

    /// <summary>
    /// Starts the polling loop in the background. Idempotent — a second call is a no-op.
    /// </summary>
    public void Start()
    {
        if (_pollTask is not null) return;
        _pollTask = Task.Run(() => RunPollLoopAsync(_cts.Token));
    }

    public async ValueTask DisposeAsync()
    {
        if (_pollTask is null)
        {
            await ReleaseLeaderLockAsync().ConfigureAwait(false);
            _cts.Dispose();
            return;
        }

        await _cts.CancelAsync().ConfigureAwait(false);

        try { await _pollTask.ConfigureAwait(false); }
        catch (OperationCanceledException) { /* expected */ }

        // Stop all runners.
        foreach (var runner in _runners.Values)
        {
            await runner.StopAsync().ConfigureAwait(false);
        }
        _runners.Clear();

        await ReleaseLeaderLockAsync().ConfigureAwait(false);
        _cts.Dispose();
    }

    // ── Poll loop ─────────────────────────────────────────────────────────────

    private async Task RunPollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_leaderLock is null)
                {
                    _leaderLock = await _db.TryAcquireSchedulerLeaderLockAsync(LeaderLockKey, ct).ConfigureAwait(false);
                    if (_leaderLock is null)
                    {
                        await Task.Delay(LeaderRetryInterval, ct).ConfigureAwait(false);
                        continue;
                    }
                }

                await SyncRunnersAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception)
            {
                // Best-effort: a transient DB failure should not permanently disable the scheduler.
                // Loop body retries on the next poll tick.
            }

            try { await Task.Delay(_pollingInterval, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task ReleaseLeaderLockAsync()
    {
        var lockHandle = _leaderLock;
        _leaderLock = null;
        if (lockHandle is not null)
        {
            try { await lockHandle.DisposeAsync().ConfigureAwait(false); }
            catch { /* best-effort */ }
        }
    }

    private async Task SyncRunnersAsync(CancellationToken ct)
    {
        // 1. DB-stored schedules
        var dbSchedules = await _db.ListSchedulesAsync(ct: ct).ConfigureAwait(false);
        var liveKeys = new HashSet<string>();

        foreach (var schedule in dbSchedules)
        {
            var key = DbScheduleKey(schedule);
            liveKeys.Add(key);

            if (!schedule.IsActive)
            {
                if (_runners.TryRemove(key, out var stopped))
                    await stopped.StopAsync().ConfigureAwait(false);
                continue;
            }

            var workflow = _executor.GetRegisteredWorkflow(schedule.WorkflowName, schedule.ClassName);
            if (workflow is null) continue;

            CronExpression cron;
            try { cron = ParseCron(schedule.Cron); }
            catch { continue; }

            _runners.GetOrAdd(key, _ => StartRunner(new RunnerConfig(
                Key: key,
                Cron: cron,
                Timezone: schedule.CronTimezone ?? TimeZoneInfo.Local,
                Workflow: workflow,
                Schedule: schedule,
                Source: ScheduleSource.Database)));
        }

        // 2. Annotated [Scheduled] workflows
        foreach (var workflow in _executor.GetRegisteredWorkflows())
        {
            var attr = workflow.WorkflowMethod.GetCustomAttribute<ScheduledAttribute>();
            if (attr is null) continue;

            var key = AnnotatedScheduleKey(workflow);
            liveKeys.Add(key);

            CronExpression cron;
            try { cron = ParseCron(attr.Cron); }
            catch { continue; }

            _runners.GetOrAdd(key, _ => StartRunner(new RunnerConfig(
                Key: key,
                Cron: cron,
                Timezone: TimeZoneInfo.Local,
                Workflow: workflow,
                Schedule: null,
                Source: ScheduleSource.Annotated,
                AnnotatedQueue: string.IsNullOrEmpty(attr.Queue) ? Constants.DbosInternalQueue : attr.Queue)));
        }

        // 3. Stop runners whose schedules disappeared.
        foreach (var stale in _runners.Keys.Where(k => !liveKeys.Contains(k)).ToList())
        {
            if (_runners.TryRemove(stale, out var runner))
                await runner.StopAsync().ConfigureAwait(false);
        }
    }

    // ── Per-schedule runner ───────────────────────────────────────────────────

    private ScheduleRunner StartRunner(RunnerConfig config)
    {
        var runner = new ScheduleRunner(config, this);
        runner.Start();
        return runner;
    }

    private TimeSpan ApplyJitter(TimeSpan baseDelay)
    {
        if (baseDelay <= TimeSpan.Zero) return TimeSpan.Zero;
        // Jitter ≤ min(10%, 10s) — matches Java SchedulerService.scheduleTask.
        var tenPct = TimeSpan.FromTicks(baseDelay.Ticks / 10);
        var maxJitter = tenPct < MaxJitter ? tenPct : MaxJitter;
        if (maxJitter <= TimeSpan.Zero) return baseDelay;
        long jitterTicks;
        lock (_jitterRng)
        {
            jitterTicks = (long)(_jitterRng.NextDouble() * maxJitter.Ticks);
        }
        return baseDelay + TimeSpan.FromTicks(jitterTicks);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CronExpression ParseCron(string expr)
    {
        // Java cron-utils SPRING53 accepts both 5-field (no seconds) and 6-field (with seconds).
        // Cronos requires the format upfront — try 6-field first, fall back to 5-field.
        try { return CronExpression.Parse(expr, CronFormat.IncludeSeconds); }
        catch { return CronExpression.Parse(expr); }
    }

    private static string DbScheduleKey(WorkflowSchedule s) => $"db:{s.Id ?? s.ScheduleName}";
    private static string AnnotatedScheduleKey(RegisteredWorkflow w) => $"ann:{w.FqName}";

    // ── Internal types ────────────────────────────────────────────────────────

    private enum ScheduleSource { Database, Annotated }

    private sealed record RunnerConfig(
        string Key,
        CronExpression Cron,
        TimeZoneInfo Timezone,
        RegisteredWorkflow Workflow,
        WorkflowSchedule? Schedule,
        ScheduleSource Source,
        string? AnnotatedQueue = null);

    private sealed class ScheduleRunner : IDisposable
    {
        private readonly RunnerConfig _config;
        private readonly SchedulerService _parent;
        private readonly CancellationTokenSource _cts = new();
        private Task? _loop;

        public ScheduleRunner(RunnerConfig config, SchedulerService parent)
        {
            _config = config;
            _parent = parent;
        }

        public void Start()
        {
            _loop = Task.Run(() => RunLoopAsync(_cts.Token));
        }

        public async Task StopAsync()
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            if (_loop is not null)
            {
                try { await _loop.ConfigureAwait(false); }
                catch (OperationCanceledException) { /* expected */ }
            }
            _cts.Dispose();
        }

        public void Dispose() => _cts.Dispose();

        private async Task RunLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var nextFireUtc = _config.Cron.GetNextOccurrence(DateTimeOffset.UtcNow, _config.Timezone);
                if (nextFireUtc is null) return; // cron exhausted

                var delay = nextFireUtc.Value - DateTimeOffset.UtcNow;
                if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
                delay = _parent.ApplyJitter(delay);

                try { await Task.Delay(delay, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }

                if (_parent._paused || ct.IsCancellationRequested) continue;

                try
                {
                    await FireAsync(nextFireUtc.Value, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { return; }
                catch (Exception)
                {
                    // Best-effort: a single failed fire should not break the loop.
                }
            }
        }

        private async Task FireAsync(DateTimeOffset scheduledUtc, CancellationToken ct)
        {
            string scheduleName = _config.Source == ScheduleSource.Database
                ? _config.Schedule!.ScheduleName
                : _config.Workflow.FqName;
            string workflowId = $"sched-{scheduleName}-{scheduledUtc:o}";

            string? queueName = _config.Source == ScheduleSource.Database
                ? (_config.Schedule!.QueueName ?? Constants.DbosInternalQueue)
                : _config.AnnotatedQueue;

            object?[] args = _config.Source == ScheduleSource.Database
                ? new object?[] { scheduledUtc, _config.Schedule!.Context }
                : new object?[] { scheduledUtc, DateTimeOffset.UtcNow };

            var options = new StartWorkflowOptions
            {
                WorkflowId = workflowId,
                QueueName = queueName,
                AppVersion = _parent._executor.LatestApplicationVersion,
            };

            await _parent._executor.StartWorkflowAsync<object?>(_config.Workflow, args, options, ct: ct).ConfigureAwait(false);

            if (_config.Source == ScheduleSource.Database)
            {
                try { await _parent._db.UpdateScheduleLastFiredAtAsync(scheduleName, scheduledUtc, ct).ConfigureAwait(false); }
                catch { /* best-effort */ }
            }
        }
    }
}
