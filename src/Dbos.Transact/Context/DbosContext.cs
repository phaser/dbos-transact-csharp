using Dbos.Transact.Workflow;
using Timeout = Dbos.Transact.Workflow.Timeout;

namespace Dbos.Transact.Context;

/// <summary>
/// Holds per-workflow-execution state: current workflow ID, step counter, timeout, deadline,
/// parent info, and pending options for the next child workflow call.
/// Port of Java <c>DBOSContext</c>.
/// </summary>
public sealed class DbosContext
{
    // Pending options consumed on the next child workflow invocation.
    internal string? NextWorkflowId;
    internal Timeout? NextTimeout;
    internal DateTimeOffset? NextDeadline;

    private readonly string? _workflowId;
    private int _functionId;
    private int? _stepFunctionId;
    private readonly WorkflowInfo? _parent;
    private readonly TimeSpan? _timeout;
    private readonly DateTimeOffset? _deadline;
    private SerializationStrategy _serialization;

    /// <summary>Creates an empty (outside-workflow) context.</summary>
    public DbosContext()
    {
        _workflowId = null;
        _functionId = -1;
        _parent = null;
        _timeout = null;
        _deadline = null;
        _serialization = SerializationStrategy.Default;
    }

    public DbosContext(string workflowId, WorkflowInfo? parent, TimeSpan? timeout, DateTimeOffset? deadline)
        : this(workflowId, parent, timeout, deadline, null) { }

    public DbosContext(
        string workflowId,
        WorkflowInfo? parent,
        TimeSpan? timeout,
        DateTimeOffset? deadline,
        SerializationStrategy? serialization)
    {
        _workflowId = workflowId;
        _functionId = 0;
        _parent = parent;
        _timeout = timeout;
        _deadline = deadline;
        _serialization = serialization ?? SerializationStrategy.Default;
    }

    /// <summary>Creates a copy of <paramref name="other"/>, optionally overriding the function ID.</summary>
    public DbosContext(DbosContext other, int? functionId = null)
    {
        NextWorkflowId = other.NextWorkflowId;
        NextTimeout = other.NextTimeout;
        NextDeadline = other.NextDeadline;
        _workflowId = other._workflowId;
        _functionId = functionId ?? other._functionId;
        _stepFunctionId = other._stepFunctionId;
        _parent = other._parent;
        _timeout = other._timeout;
        _deadline = other._deadline;
        _serialization = other._serialization;
    }

    public bool IsInWorkflow => _workflowId != null;
    public bool IsInStep => _stepFunctionId.HasValue;

    public string? WorkflowId => _workflowId;
    public int? StepId => _stepFunctionId;
    public int CurrentFunctionId => _functionId;

    public int GetAndIncrementFunctionId() => _functionId++;

    public void SetStepFunctionId(int functionId) => _stepFunctionId = functionId;
    public void ResetStepFunctionId() => _stepFunctionId = null;

    public WorkflowInfo? Parent => _parent;
    public TimeSpan? Timeout => _timeout;
    public DateTimeOffset? Deadline => _deadline;
    public SerializationStrategy Serialization => _serialization;

    public void SetSerializationStrategy(SerializationStrategy strategy) => _serialization = strategy;

    /// <summary>
    /// Consumes and returns the pending next workflow ID, then clears it.
    /// Falls back to <paramref name="fallback"/> if no pending ID is set.
    /// </summary>
    public string? ConsumeNextWorkflowId(string? fallback = null)
    {
        if (NextWorkflowId != null)
        {
            var value = NextWorkflowId;
            NextWorkflowId = null;
            return value;
        }
        return fallback;
    }

    // Static accessors delegating to the ambient context.
    public static string? GetWorkflowId() => DbosContextHolder.Get()?.WorkflowId;
    public static int? GetStepId() => DbosContextHolder.Get()?.StepId;
    public static bool InWorkflow() => DbosContextHolder.Get()?.IsInWorkflow ?? false;
    public static bool InStep() => DbosContextHolder.Get()?.IsInStep ?? false;
    public static SerializationStrategy? GetSerializationStrategy() => DbosContextHolder.Get()?.Serialization;
}
