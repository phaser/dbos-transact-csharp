namespace Dbos.Transact.Execution;

public sealed record RegisteredWorkflowInstance(string ClassName, string? InstanceName, object Target)
{
    public static string FullyQualifiedInstName(string className, string? instanceName) =>
        $"{className}/{instanceName ?? ""}";
}
