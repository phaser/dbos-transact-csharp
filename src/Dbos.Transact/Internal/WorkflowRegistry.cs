using System.Collections.Concurrent;
using System.Reflection;
using Dbos.Transact.Execution;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Internal;

public sealed class WorkflowRegistry
{
    private readonly ConcurrentDictionary<string, RegisteredWorkflowInstance> _instanceRegistry = new();
    private readonly ConcurrentDictionary<string, RegisteredWorkflow> _workflowRegistry = new();

    public static string GetWorkflowClassName(object target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var type = target.GetType();
        var attr = type.GetCustomAttribute<WorkflowClassNameAttribute>();
        return attr is null || string.IsNullOrEmpty(attr.Value)
            ? type.FullName ?? type.Name
            : attr.Value;
    }

    public static string GetWorkflowName(WorkflowAttribute wfAttr, MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(wfAttr);
        ArgumentNullException.ThrowIfNull(method);
        return string.IsNullOrEmpty(wfAttr.Name) ? method.Name : wfAttr.Name;
    }

    public void RegisterInstance(string? instanceName, object target)
    {
        var className = GetWorkflowClassName(target);
        var fqName = RegisteredWorkflowInstance.FullyQualifiedInstName(className, instanceName);
        var entry = new RegisteredWorkflowInstance(className, instanceName, target);
        if (!_instanceRegistry.TryAdd(fqName, entry))
            throw new InvalidOperationException($"Workflow class already registered with name: {fqName}");
    }

    public void RegisterWorkflow(WorkflowAttribute wfAttr, object target, MethodInfo method, string? instanceName)
    {
        var workflowName = GetWorkflowName(wfAttr, method);
        var className = GetWorkflowClassName(target);
        var fqName = RegisteredWorkflow.FullyQualifiedName(workflowName, className, instanceName);
        var entry = new RegisteredWorkflow(
            workflowName, className, instanceName, target, method,
            wfAttr.MaxRecoveryAttempts, wfAttr.SerializationStrategy);
        if (!_workflowRegistry.TryAdd(fqName, entry))
            throw new InvalidOperationException($"Workflow already registered with name: {fqName}");
    }

    public IReadOnlyDictionary<string, RegisteredWorkflow> GetWorkflowSnapshot() =>
        new Dictionary<string, RegisteredWorkflow>(_workflowRegistry);

    public IReadOnlyDictionary<string, RegisteredWorkflowInstance> GetInstanceSnapshot() =>
        new Dictionary<string, RegisteredWorkflowInstance>(_instanceRegistry);
}
