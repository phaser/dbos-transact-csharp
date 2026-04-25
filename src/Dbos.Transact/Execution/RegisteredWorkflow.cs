using System.Reflection;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Execution;

public sealed record RegisteredWorkflow(
    string WorkflowName,
    string ClassName,
    string? InstanceName,
    object Target,
    MethodInfo WorkflowMethod,
    int MaxRecoveryAttempts,
    SerializationStrategy SerializationStrategy)
{
    public static string FullyQualifiedName(string workflowName, string className, string? instanceName = null) =>
        $"{workflowName}/{className}/{instanceName ?? ""}";

    public string FqName => FullyQualifiedName(WorkflowName, ClassName, InstanceName);
}
