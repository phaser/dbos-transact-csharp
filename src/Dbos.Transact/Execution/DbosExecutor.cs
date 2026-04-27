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
    private readonly ConcurrentDictionary<string, bool> _workflowsInProgress = new();

    public DbosExecutor(
        SystemDatabase db,
        IDbosSerializer serializer,
        string? executorId = null,
        string? appVersion = null,
        string? appId = null)
    {
        _db = db;
        _serializer = serializer;
        _executorId = executorId ?? Guid.NewGuid().ToString();
        _appVersion = appVersion;
        _appId = appId;
    }

    public string ExecutorId => _executorId;

    // ── Public API ────────────────────────────────────────────────────────────

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
            initStatus, maxRetries, isRecoveryRequest: false, isDequeuedRequest: false, ct)
            .ConfigureAwait(false);

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
