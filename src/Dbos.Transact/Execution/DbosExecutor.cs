using System.Collections.Concurrent;
using System.Reflection;
using Dbos.Transact.Context;
using Dbos.Transact.Database;
using Dbos.Transact.Internal;
using Dbos.Transact.Json;
using Dbos.Transact.Workflow;
using Dbos.Transact.Workflow.Internal;
using WorkflowTimeout = Dbos.Transact.Workflow.Timeout;

namespace Dbos.Transact.Execution;

/// <summary>
/// Core durable-workflow engine. Initialises workflow state in the system database,
/// executes the workflow body on a background task, checkpoints each step, and supports
/// idempotent replay on crash-recovery.
/// Port of Java's <c>DBOSExecutor</c>.
/// </summary>
public sealed class DbosExecutor : IAsyncDisposable
{
    private readonly SystemDatabase _db;
    private readonly IDbosSerializer _serializer;
    private readonly string _executorId;
    private readonly string? _appVersion;
    private readonly string? _appId;
    private readonly QueueRegistry _queueRegistry;
    private readonly ConcurrentDictionary<string, bool> _workflowsInProgress = new();
    private readonly ConcurrentDictionary<string, RegisteredWorkflow> _workflowMap = new();

    public DbosExecutor(
        SystemDatabase db,
        IDbosSerializer serializer,
        string? executorId = null,
        string? appVersion = null,
        string? appId = null,
        QueueRegistry? queueRegistry = null)
    {
        _db = db;
        _serializer = serializer;
        _executorId = executorId ?? Guid.NewGuid().ToString();
        _appVersion = appVersion;
        _appId = appId;
        _queueRegistry = queueRegistry ?? new QueueRegistry();
    }

    public string ExecutorId => _executorId;

    /// <summary>App version reported on workflows started by this executor.</summary>
    public string? LatestApplicationVersion => _appVersion;

    /// <summary>Looks up a workflow by its fully qualified name (workflowName, className, instanceName).</summary>
    public RegisteredWorkflow? GetRegisteredWorkflow(string workflowName, string? className, string? instanceName = null)
    {
        var fq = RegisteredWorkflow.FullyQualifiedName(workflowName, className ?? string.Empty, instanceName);
        return _workflowMap.TryGetValue(fq, out var wf) ? wf : null;
    }

    /// <summary>Returns all registered workflows.</summary>
    public IReadOnlyCollection<RegisteredWorkflow> GetRegisteredWorkflows() => _workflowMap.Values.ToArray();

    /// <summary>Looks up a queue registered on this executor.</summary>
    public Queue? GetQueue(string queueName) => _queueRegistry.Get(queueName);

    /// <summary>Reads the durable external-state value for the given (service, workflow, key).</summary>
    public Task<ExternalState?> GetExternalStateAsync(string service, string workflowName, string key, CancellationToken ct = default) =>
        _db.GetExternalStateAsync(service, workflowName, key, ct);

    /// <summary>Upserts a durable external-state value, returning the post-upsert state.</summary>
    public Task<ExternalState> UpsertExternalStateAsync(ExternalState state, CancellationToken ct = default) =>
        _db.UpsertExternalStateAsync(state, ct);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Registers a workflow so it can be found by <see cref="ExecuteWorkflowByIdAsync"/>.</summary>
    public void RegisterWorkflow(RegisteredWorkflow workflow) =>
        _workflowMap.TryAdd(workflow.FqName, workflow);

    /// <summary>
    /// Durably starts a workflow: writes the DB record, then executes the workflow body
    /// on a background task. Returns a handle that resolves to the workflow result.
    /// Port of Java's <c>executeWorkflow</c>.
    /// </summary>
    public async Task<WorkflowHandle<T>> StartWorkflowAsync<T>(
        RegisteredWorkflow workflow,
        object?[]? args = null,
        StartWorkflowOptions? options = null,
        WorkflowInfo? parent = null,
        bool isDequeuedRequest = false,
        CancellationToken ct = default)
    {
        var opts = options ?? new StartWorkflowOptions();
        var workflowId = opts.WorkflowId ?? Guid.NewGuid().ToString();

        var inputs = _serializer.Serialize(args);

        TimeSpan? timeout = opts.Timeout switch
        {
            WorkflowTimeout.Explicit exp => exp.Value,
            WorkflowTimeout.None => null,
            _ => null,
        };

        var initStatus = new WorkflowStatusInternal(
            WorkflowId: workflowId,
            WorkflowName: workflow.WorkflowName,
            ClassName: workflow.ClassName,
            InstanceName: workflow.InstanceName,
            QueueName: opts.QueueName,
            DeduplicationId: opts.DeduplicationId,
            Priority: opts.Priority,
            QueuePartitionKey: opts.QueuePartitionKey,
            Delay: opts.Delay,
            AuthenticatedUser: null,
            AssumedRole: null,
            AuthenticatedRoles: null,
            Inputs: inputs,
            ExecutorId: _executorId,
            AppVersion: opts.AppVersion ?? _appVersion,
            AppId: _appId,
            Timeout: timeout,
            Deadline: opts.Deadline,
            ParentWorkflowId: parent?.WorkflowId,
            Serialization: _serializer.Name);

        // maxRetries ≤ 0 means unlimited
        var maxRetries = workflow.MaxRecoveryAttempts > 0 ? workflow.MaxRecoveryAttempts : int.MaxValue;

        var initResult = await _db.InitWorkflowStatusAsync(
            initStatus, maxRetries, isRecoveryRequest: false, isDequeuedRequest: isDequeuedRequest, ct)
            .ConfigureAwait(false);

        // Queued workflows wait for the QueueService to dequeue them; don't run the body here.
        if (opts.QueueName is not null)
            return new WorkflowHandleDbPoll<T>(_db, _serializer, workflowId);

        if (!initResult.ShouldExecuteOnThisExecutor || initResult.Status == WorkflowState.Success)
            return new WorkflowHandleDbPoll<T>(_db, _serializer, workflowId);

        // Already past deadline — cancel immediately
        if (initResult.DeadlineEpochMs.HasValue
            && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > initResult.DeadlineEpochMs.Value)
        {
            await _db.CancelWorkflowsAsync([workflowId], CancellationToken.None).ConfigureAwait(false);
            return new WorkflowHandleDbPoll<T>(_db, _serializer, workflowId);
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        _ = Task.Run(async () =>
        {
            if (!_workflowsInProgress.TryAdd(workflowId, true))
                return;

            try
            {
                DbosContextHolder.Set(new DbosContext(
                    workflowId, parent, timeout, opts.Deadline, workflow.SerializationStrategy));

                T output = await InvokeWorkflowAsync<T>(workflow, args).ConfigureAwait(false);

                var serializedOutput = _serializer.Serialize(output);
                await _db.RecordWorkflowOutputAsync(workflowId, serializedOutput).ConfigureAwait(false);

                tcs.SetResult(output);
            }
            catch (Exception ex)
            {
                try
                {
                    var serializedError = _serializer.SerializeException(ex);
                    await _db.RecordWorkflowErrorAsync(workflowId, serializedError).ConfigureAwait(false);
                }
                catch
                {
                    // best-effort: DB write failed, but we still surface the original exception
                }

                tcs.SetException(ex);
            }
            finally
            {
                DbosContextHolder.Clear();
                _workflowsInProgress.TryRemove(workflowId, out _);
            }
        }, CancellationToken.None);

        return new WorkflowHandleTcs<T>(workflowId, tcs, _db);
    }

    /// <summary>
    /// Loads a previously-enqueued or crashed workflow from the database by ID and re-executes it.
    /// The caller is responsible for ensuring the workflow is already in a runnable state (PENDING or ENQUEUED).
    /// Port of Java's <c>executeWorkflowById</c>.
    /// </summary>
    public async Task ExecuteWorkflowByIdAsync(
        string workflowId,
        bool isRecoveryRequest,
        bool isDequeuedRequest,
        CancellationToken ct = default)
    {
        var status = await _db.GetWorkflowStatusAsync(workflowId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Workflow not found: {workflowId}");

        var fqName = RegisteredWorkflow.FullyQualifiedName(
            status.WorkflowName ?? string.Empty,
            status.ClassName ?? string.Empty,
            status.InstanceName);

        if (!_workflowMap.TryGetValue(fqName, out var workflow))
            throw new InvalidOperationException(
                $"Workflow '{fqName}' is not registered on this executor.");

        var rawInputs = await _db.GetWorkflowInputsAsync(workflowId, ct).ConfigureAwait(false);
        var inputs = rawInputs is not null
            ? (object?[]?)_serializer.Deserialize(rawInputs)
            : null;

        var opts = new StartWorkflowOptions { WorkflowId = workflowId };

        await StartWorkflowAsync<object?>(
            workflow, inputs, opts,
            isDequeuedRequest: isDequeuedRequest, ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates an invocation handler suitable for use with <see cref="DbosProxyFactory"/>.
    /// The returned handler intercepts <c>[Step]</c> calls and checkpoints them durably.
    /// </summary>
    public DbosInvocationInterceptor.InvocationHandler CreateInvocationHandler() =>
        HandleInvocationAsync;

    // ── Invocation handler ────────────────────────────────────────────────────

    private async Task<object?> HandleInvocationAsync(
        object target, string? instanceName, MethodInfo method, object?[] args, CancellationToken ct)
    {
        var isStep = method.IsDefined(typeof(StepAttribute), inherit: true);

        if (isStep)
            return await ExecuteStepAsync(target, method, args, ct).ConfigureAwait(false);

        // [Workflow] call via proxy — child-workflow support (future wave)
        throw new NotSupportedException(
            $"Calling [{nameof(WorkflowAttribute)}] methods directly through the proxy is not yet supported. " +
            $"Use {nameof(StartWorkflowAsync)} to start a workflow.");
    }

    // ── Step execution ────────────────────────────────────────────────────────

    private async Task<object?> ExecuteStepAsync(
        object target, MethodInfo method, object?[] args, CancellationToken ct)
    {
        var ctx = DbosContextHolder.Get()
            ?? throw new InvalidOperationException(
                "Steps can only be called from within a workflow execution context.");

        var workflowId = ctx.WorkflowId
            ?? throw new InvalidOperationException("WorkflowId is not set in the current context.");

        var functionId = ctx.GetAndIncrementFunctionId();
        var stepAttr = method.GetCustomAttribute<StepAttribute>()!;
        var functionName = string.IsNullOrEmpty(stepAttr.Name) ? method.Name : stepAttr.Name;

        // Check for a previously recorded result (idempotent replay)
        var prev = await _db.CheckStepExecutionAsync(workflowId, functionId, functionName, ct)
            .ConfigureAwait(false);

        if (prev.Error is not null)
        {
            var ex = _serializer.DeserializeException(prev.Error)
                ?? new InvalidOperationException($"Step '{functionName}' (id={functionId}) previously failed.");
            throw ex;
        }

        if (prev.Output is not null)
            return _serializer.Deserialize(prev.Output);

        // New step — execute with retry
        var maxAttempts = stepAttr.RetriesAllowed ? stepAttr.MaxAttempts : 1;
        var retryInterval = TimeSpan.FromSeconds(stepAttr.IntervalSeconds);
        var startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                ctx.SetStepFunctionId(functionId);
                object? rawOutput = method.Invoke(target, args);
                object? output = await UnwrapTaskAsync(rawOutput).ConfigureAwait(false);
                ctx.ResetStepFunctionId();

                var serializedOutput = _serializer.Serialize(output);
                var stepResult = new StepResult(workflowId, functionId, functionName,
                    Output: serializedOutput, Serialization: _serializer.Name);
                await _db.RecordStepResultAsync(stepResult, startTime, ct).ConfigureAwait(false);

                return output;
            }
            catch (TargetInvocationException tie) when (tie.InnerException is not null)
            {
                lastException = tie.InnerException;
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
            finally
            {
                ctx.ResetStepFunctionId();
            }

            if (attempt < maxAttempts)
                await Task.Delay(retryInterval, ct).ConfigureAwait(false);

            retryInterval = TimeSpan.FromTicks((long)(retryInterval.Ticks * stepAttr.BackOffRate));
        }

        // All attempts exhausted — record and rethrow
        var serializedError = _serializer.SerializeException(lastException!);
        var errorResult = new StepResult(workflowId, functionId, functionName,
            Error: serializedError, Serialization: _serializer.Name);
        await _db.RecordStepResultAsync(errorResult, startTime, ct).ConfigureAwait(false);

        throw lastException!;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<T> InvokeWorkflowAsync<T>(RegisteredWorkflow workflow, object?[]? args)
    {
        var rawReturn = workflow.WorkflowMethod.Invoke(workflow.Target, args);
        var unwrapped = await UnwrapTaskAsync(rawReturn).ConfigureAwait(false);
        return (T)unwrapped!;
    }

    private static async Task<object?> UnwrapTaskAsync(object? value)
    {
        if (value is null)
            return null;

        if (value is Task task)
        {
            await task.ConfigureAwait(false);

            var taskType = task.GetType();
            if (taskType.IsGenericType)
            {
                var resultProp = taskType.GetProperty("Result");
                return resultProp?.GetValue(task);
            }
            return null;
        }

        return value;
    }

    // ── IAsyncDisposable ──────────────────────────────────────────────────────

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
