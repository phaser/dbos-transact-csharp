using System.Reflection;
using Dbos.Transact.Context;
using Dbos.Transact.Database;
using Dbos.Transact.Execution;
using Dbos.Transact.Internal;
using Dbos.Transact.Json;
using Dbos.Transact.Workflow;
using Dbos.Transact.Workflow.Internal;

namespace Dbos.Transact;

/// <summary>
/// Public facade for DBOS — port of Java's <c>DBOS</c> class. Owns the workflow / queue
/// registries, lifecycle listeners, and (after <see cref="LaunchAsync"/>) the executor,
/// queue service, and scheduler service. Use <see cref="Builder(string)"/> or
/// <see cref="Builder(DbosOptions)"/> together with a dialect extension
/// (e.g. <c>UsePostgres</c>, <c>UseSqlite</c>) to construct an instance.
/// </summary>
public sealed class Dbos : IAsyncDisposable
{
    private readonly DbosOptions _options;
    private readonly Func<IDbosSerializer, SystemDatabase> _systemDatabaseFactory;
    private readonly Func<DbosOptions, CancellationToken, Task>? _migrationRunner;
    private readonly WorkflowRegistry _workflowRegistry = new();
    private readonly QueueRegistry _queueRegistry = new();
    private readonly HashSet<IDbosLifecycleListener> _lifecycleListeners = [];
    private readonly object _gate = new();

    private SystemDatabase? _systemDatabase;
    private DbosExecutor? _executor;
    private QueueService? _queueService;
    private SchedulerService? _schedulerService;
    private IDbosSerializer? _serializer;
    private bool _launched;
    private bool _disposed;

    internal Dbos(
        DbosOptions options,
        Func<IDbosSerializer, SystemDatabase> systemDatabaseFactory,
        Func<DbosOptions, CancellationToken, Task>? migrationRunner)
    {
        _options = options;
        _systemDatabaseFactory = systemDatabaseFactory;
        _migrationRunner = migrationRunner;
    }

    public DbosOptions Options => _options;
    public bool IsLaunched => _launched;

    /// <summary>Begins building a new <see cref="Dbos"/> with default options.</summary>
    public static DbosBuilder Builder(string appName) => new(DbosOptions.Defaults(appName));

    /// <summary>Begins building a new <see cref="Dbos"/> from explicit options.</summary>
    public static DbosBuilder Builder(DbosOptions options) => new(options);

    // ── Pre-launch registration ───────────────────────────────────────────────

    /// <summary>Registers a lifecycle listener. Must be called before <see cref="LaunchAsync"/>.</summary>
    public void RegisterLifecycleListener(IDbosLifecycleListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        ThrowIfLaunched(nameof(RegisterLifecycleListener));
        lock (_gate) { _lifecycleListeners.Add(listener); }
    }

    /// <summary>Registers a queue. Must be called before <see cref="LaunchAsync"/>.</summary>
    public void RegisterQueue(Queue queue)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ThrowIfLaunched(nameof(RegisterQueue));
        _queueRegistry.Register(queue);
    }

    /// <summary>Registers multiple queues. Must be called before <see cref="LaunchAsync"/>.</summary>
    public void RegisterQueues(params Queue[] queues)
    {
        ArgumentNullException.ThrowIfNull(queues);
        foreach (var q in queues) RegisterQueue(q);
    }

    /// <summary>
    /// Registers all <c>[Workflow]</c> methods declared on <paramref name="target"/>'s concrete type
    /// and returns a Castle.DynamicProxy interface proxy that intercepts <c>[Step]</c> calls.
    /// Must be called before <see cref="LaunchAsync"/>.
    /// Mirrors Java's <c>DBOS.registerProxy</c>.
    /// </summary>
    public T RegisterProxy<T>(T target, string? instanceName = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(target);
        ThrowIfLaunched(nameof(RegisterProxy));

        if (!HasWorkflowsOrSteps(target))
            throw new ArgumentException(
                $"Target type {target.GetType().FullName} has no [Workflow] or [Step] methods.",
                nameof(target));

        _workflowRegistry.RegisterInstance(instanceName, target);

        foreach (var method in target.GetType().GetMethods(
                     BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var wfAttr = method.GetCustomAttribute<WorkflowAttribute>(inherit: true);
            if (wfAttr is null) continue;
            _workflowRegistry.RegisterWorkflow(wfAttr, target, method, instanceName);
        }

        return DbosProxyFactory.CreateProxy(target, instanceName, CreateLazyInvocationHandler());
    }

    /// <summary>
    /// Registers a single workflow method. Most callers should use <see cref="RegisterProxy{T}(T, string?)"/>;
    /// this overload exists for advanced integration scenarios.
    /// </summary>
    public void RegisterWorkflow(WorkflowAttribute wfAttr, object target, MethodInfo method, string? instanceName)
    {
        ArgumentNullException.ThrowIfNull(wfAttr);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(method);
        ThrowIfLaunched(nameof(RegisterWorkflow));
        _workflowRegistry.RegisterWorkflow(wfAttr, target, method, instanceName);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Launches DBOS: runs migrations (if enabled), opens the system database, starts the
    /// executor, queue service, and scheduler service, and notifies lifecycle listeners.
    /// Idempotent — a second call is a no-op.
    /// </summary>
    public async Task LaunchAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_launched) return;

        _serializer = _options.Serializer ?? DbosJsonSerializer.Instance;

        if (_options.Migrate && _migrationRunner is not null)
            await _migrationRunner(_options, ct).ConfigureAwait(false);

        _systemDatabase = _systemDatabaseFactory(_serializer);
        await _systemDatabase.StartAsync(ct).ConfigureAwait(false);

        // Register all queues (including user-registered ones) into a fresh executor.
        _executor = new DbosExecutor(
            _systemDatabase,
            _serializer,
            executorId: _options.ExecutorId,
            appVersion: _options.AppVersion,
            appId: null,
            queueRegistry: _queueRegistry);

        foreach (var rw in _workflowRegistry.GetWorkflowSnapshot().Values)
            _executor.RegisterWorkflow(rw);

        _queueService = new QueueService(_executor, _systemDatabase);
        _queueService.Start(_queueRegistry.GetSnapshot(), _options.ListenQueues);

        _schedulerService = new SchedulerService(_executor, _systemDatabase, _options.SchedulerPollingInterval);
        _schedulerService.Start();

        _launched = true;

        IDbosLifecycleListener[] snapshot;
        lock (_gate) { snapshot = [.. _lifecycleListeners]; }
        foreach (var l in snapshot) l.DbosLaunched();
    }

    /// <summary>Shuts DBOS down: stops services, disposes the executor and system database.</summary>
    public async Task ShutdownAsync()
    {
        if (!_launched) return;

        IDbosLifecycleListener[] snapshot;
        lock (_gate) { snapshot = [.. _lifecycleListeners]; }
        foreach (var l in snapshot)
        {
            try { l.DbosShutDown(); } catch { /* best-effort */ }
        }

        if (_schedulerService is not null)
        {
            await _schedulerService.DisposeAsync().ConfigureAwait(false);
            _schedulerService = null;
        }

        if (_queueService is not null)
        {
            await _queueService.DisposeAsync().ConfigureAwait(false);
            _queueService = null;
        }

        if (_executor is not null)
        {
            await _executor.DisposeAsync().ConfigureAwait(false);
            _executor = null;
        }

        if (_systemDatabase is not null)
        {
            await _systemDatabase.DisposeAsync().ConfigureAwait(false);
            _systemDatabase = null;
        }

        _launched = false;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await ShutdownAsync().ConfigureAwait(false);
    }

    // ── Workflow lifecycle ────────────────────────────────────────────────────

    /// <summary>
    /// Starts a workflow that has been registered via <see cref="RegisterProxy{T}(T, string?)"/>
    /// or <see cref="RegisterWorkflow"/>. Looks up the workflow by (workflowName, className, instanceName).
    /// </summary>
    public Task<WorkflowHandle<T>> StartWorkflowAsync<T>(
        string workflowName,
        string? className,
        string? instanceName,
        object?[]? args,
        StartWorkflowOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowName);
        var executor = EnsureLaunched(nameof(StartWorkflowAsync));
        var rw = executor.GetRegisteredWorkflow(workflowName, className, instanceName)
            ?? throw new InvalidOperationException(
                $"Workflow '{workflowName}' (class={className}, instance={instanceName}) is not registered.");
        return executor.StartWorkflowAsync<T>(rw, args, options, ct: ct);
    }

    /// <summary>Starts a previously-resolved workflow.</summary>
    public Task<WorkflowHandle<T>> StartWorkflowAsync<T>(
        RegisteredWorkflow workflow,
        object?[]? args = null,
        StartWorkflowOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        var executor = EnsureLaunched(nameof(StartWorkflowAsync));
        return executor.StartWorkflowAsync<T>(workflow, args, options, ct: ct);
    }

    /// <summary>
    /// Returns a <see cref="WorkflowHandle{T}"/> for the given workflow ID. The handle exists
    /// even if the workflow does not — use <see cref="WorkflowHandle{T}.GetStatusAsync"/> to tell.
    /// </summary>
    public WorkflowHandle<T> RetrieveWorkflow<T>(string workflowId)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowId);
        EnsureLaunched(nameof(RetrieveWorkflow));
        return new WorkflowHandleDbPoll<T>(_systemDatabase!, _serializer!, workflowId);
    }

    /// <summary>Polls the database until the workflow completes, then returns its result.</summary>
    public Task<T> GetResultAsync<T>(string workflowId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowId);
        EnsureLaunched(nameof(GetResultAsync));
        return _systemDatabase!.AwaitWorkflowResultAsync<T>(workflowId, _serializer!, ct);
    }

    /// <summary>Returns the current status of a workflow, or <c>null</c> if no such workflow exists.</summary>
    public Task<WorkflowStatus?> GetWorkflowStatusAsync(string workflowId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowId);
        EnsureLaunched(nameof(GetWorkflowStatusAsync));
        return _systemDatabase!.GetWorkflowStatusAsync(workflowId, ct);
    }

    // ── Workflow management ───────────────────────────────────────────────────

    public Task CancelWorkflowAsync(string workflowId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowId);
        EnsureLaunched(nameof(CancelWorkflowAsync));
        return _systemDatabase!.CancelWorkflowsAsync([workflowId], ct);
    }

    public Task CancelWorkflowsAsync(IReadOnlyList<string> workflowIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workflowIds);
        EnsureLaunched(nameof(CancelWorkflowsAsync));
        return _systemDatabase!.CancelWorkflowsAsync(workflowIds, ct);
    }

    public async Task<WorkflowHandle<T>> ResumeWorkflowAsync<T>(
        string workflowId, string? queueName = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowId);
        EnsureLaunched(nameof(ResumeWorkflowAsync));
        await _systemDatabase!.ResumeWorkflowsAsync([workflowId], queueName, ct).ConfigureAwait(false);
        return RetrieveWorkflow<T>(workflowId);
    }

    public Task DeleteWorkflowAsync(string workflowId, bool deleteChildren = false, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowId);
        EnsureLaunched(nameof(DeleteWorkflowAsync));
        return _systemDatabase!.DeleteWorkflowsAsync([workflowId], deleteChildren, ct);
    }

    public Task DeleteWorkflowsAsync(
        IReadOnlyList<string> workflowIds, bool deleteChildren = false, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workflowIds);
        EnsureLaunched(nameof(DeleteWorkflowsAsync));
        return _systemDatabase!.DeleteWorkflowsAsync(workflowIds, deleteChildren, ct);
    }

    public async Task<WorkflowHandle<T>> ForkWorkflowAsync<T>(
        string workflowId, int startStep, ForkOptions? options = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowId);
        EnsureLaunched(nameof(ForkWorkflowAsync));
        var forkedId = await _systemDatabase!
            .ForkWorkflowAsync(workflowId, startStep, options ?? new ForkOptions(), ct)
            .ConfigureAwait(false);
        return RetrieveWorkflow<T>(forkedId);
    }

    // ── Listing ───────────────────────────────────────────────────────────────

    public Task<IReadOnlyList<WorkflowStatus>> ListWorkflowsAsync(
        ListWorkflowsInput? input = null, CancellationToken ct = default)
    {
        EnsureLaunched(nameof(ListWorkflowsAsync));
        return _systemDatabase!.ListWorkflowsAsync(input ?? new ListWorkflowsInput(), ct);
    }

    public Task<IReadOnlyList<StepInfo>> ListWorkflowStepsAsync(
        string workflowId, int? limit = null, int? offset = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowId);
        EnsureLaunched(nameof(ListWorkflowStepsAsync));
        return _systemDatabase!.ListWorkflowStepsAsync(workflowId, loadOutput: true, limit, offset, ct);
    }

    public IReadOnlyCollection<RegisteredWorkflow> GetRegisteredWorkflows()
    {
        var executor = EnsureLaunched(nameof(GetRegisteredWorkflows));
        return executor.GetRegisteredWorkflows();
    }

    // ── Queue lookup ──────────────────────────────────────────────────────────

    public Queue? GetQueue(string queueName)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueName);
        EnsureLaunched(nameof(GetQueue));
        return _queueRegistry.Get(queueName);
    }

    // ── Schedules ─────────────────────────────────────────────────────────────

    public Task CreateScheduleAsync(WorkflowSchedule schedule, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        EnsureLaunched(nameof(CreateScheduleAsync));
        return _systemDatabase!.CreateScheduleAsync(schedule, ct);
    }

    public Task<WorkflowSchedule?> GetScheduleAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        EnsureLaunched(nameof(GetScheduleAsync));
        return _systemDatabase!.GetScheduleAsync(name, ct);
    }

    public Task<IReadOnlyList<WorkflowSchedule>> ListSchedulesAsync(
        IReadOnlyList<ScheduleStatus>? statuses = null,
        IReadOnlyList<string>? workflowNames = null,
        IReadOnlyList<string>? namePrefixes = null,
        CancellationToken ct = default)
    {
        EnsureLaunched(nameof(ListSchedulesAsync));
        return _systemDatabase!.ListSchedulesAsync(statuses, workflowNames, namePrefixes, ct);
    }

    public Task DeleteScheduleAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        EnsureLaunched(nameof(DeleteScheduleAsync));
        return _systemDatabase!.DeleteScheduleAsync(name, ct);
    }

    public Task PauseScheduleAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        EnsureLaunched(nameof(PauseScheduleAsync));
        return _systemDatabase!.PauseScheduleAsync(name, ct);
    }

    public Task ResumeScheduleAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        EnsureLaunched(nameof(ResumeScheduleAsync));
        return _systemDatabase!.ResumeScheduleAsync(name, ct);
    }

    public Task ApplySchedulesAsync(IReadOnlyList<WorkflowSchedule> schedules, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(schedules);
        EnsureLaunched(nameof(ApplySchedulesAsync));
        return _systemDatabase!.ApplySchedulesAsync(schedules, ct);
    }

    // ── External state ────────────────────────────────────────────────────────

    public Task<ExternalState?> GetExternalStateAsync(
        string service, string workflowName, string key, CancellationToken ct = default)
    {
        EnsureLaunched(nameof(GetExternalStateAsync));
        return _systemDatabase!.GetExternalStateAsync(service, workflowName, key, ct);
    }

    public Task<ExternalState> UpsertExternalStateAsync(ExternalState state, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        EnsureLaunched(nameof(UpsertExternalStateAsync));
        return _systemDatabase!.UpsertExternalStateAsync(state, ct);
    }

    // ── Static helpers ────────────────────────────────────────────────────────

    /// <summary>Returns the workflow ID of the currently-executing workflow, or <c>null</c>.</summary>
    public static string? WorkflowId() => DbosContext.GetWorkflowId();

    /// <summary>Returns the step ID of the currently-executing step, or <c>null</c>.</summary>
    public static int? StepId() => DbosContext.GetStepId();

    /// <summary>Returns <c>true</c> if the calling thread is currently inside a workflow body.</summary>
    public static bool InWorkflow() => DbosContext.InWorkflow();

    /// <summary>Returns <c>true</c> if the calling thread is currently inside a step body.</summary>
    public static bool InStep() => DbosContext.InStep();

    // ── Internal helpers ──────────────────────────────────────────────────────

    private DbosExecutor EnsureLaunched(string caller)
    {
        if (!_launched || _executor is null)
            throw new InvalidOperationException($"Cannot call {caller} before Dbos is launched.");
        return _executor;
    }

    private void ThrowIfLaunched(string caller)
    {
        if (_launched)
            throw new InvalidOperationException($"Cannot call {caller} after Dbos is launched.");
    }

    private static bool HasWorkflowsOrSteps(object target)
    {
        var type = target.GetType();
        if (HasMatchingMethod(type)) return true;
        foreach (var iface in type.GetInterfaces())
            if (HasMatchingMethod(iface)) return true;
        return false;

        static bool HasMatchingMethod(Type t)
        {
            var methods = t.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var m in methods)
            {
                if (m.IsDefined(typeof(WorkflowAttribute), inherit: true) ||
                    m.IsDefined(typeof(StepAttribute), inherit: true))
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Builds an <see cref="DbosInvocationInterceptor.InvocationHandler"/> that resolves the
    /// executor lazily (matches Java's <c>Supplier&lt;DBOSExecutor&gt;</c> pattern), so proxies
    /// can be created before <see cref="LaunchAsync"/>.
    /// </summary>
    private DbosInvocationInterceptor.InvocationHandler CreateLazyInvocationHandler() =>
        (target, instanceName, method, args, ct) =>
        {
            var executor = _executor
                ?? throw new InvalidOperationException(
                    "Cannot invoke workflow or step methods through a registered proxy before Dbos is launched.");
            return executor.CreateInvocationHandler()(target, instanceName, method, args, ct);
        };
}
