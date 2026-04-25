namespace Dbos.Transact.Exceptions;

/// <summary>
/// Thrown when invocation of a workflow function is attempted using a registered name that does
/// not exist in the registry.
/// </summary>
public sealed class DbosWorkflowFunctionNotFoundException : DbosException
{
    public string WorkflowId { get; }
    public string WorkflowName { get; }

    public DbosWorkflowFunctionNotFoundException(string id, string name)
        : base($"Workflow function {name} does not exist for workflow id {id}.")
    {
        WorkflowId = id;
        WorkflowName = name;
    }
}
