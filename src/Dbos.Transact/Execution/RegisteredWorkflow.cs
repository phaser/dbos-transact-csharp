using System.Reflection;
using System.Runtime.ExceptionServices;
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

    /// <summary>
    /// Invokes the workflow method, unwrapping <see cref="TargetInvocationException"/> to preserve
    /// the original exception's stack trace. Port of Java's <c>RegisteredWorkflow.invoke(Object[])</c>.
    /// </summary>
    public T Invoke<T>(object?[]? args)
    {
        try
        {
            return (T)WorkflowMethod.Invoke(Target, args)!;
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
            throw; // unreachable — satisfies the compiler
        }
    }
}
