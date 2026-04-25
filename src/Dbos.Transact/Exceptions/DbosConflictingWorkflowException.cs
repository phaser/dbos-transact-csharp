namespace Dbos.Transact.Exceptions;

/// <summary>
/// Thrown when an attempt is made to start a workflow with an ID that already exists and the
/// existing workflow is incompatible with the attempted invocation.
/// </summary>
public sealed class DbosConflictingWorkflowException : DbosException
{
    public string WorkflowId { get; }

    public DbosConflictingWorkflowException(string workflowId, string msg)
        : base($"Conflicting workflow invocation with same ID {workflowId} : {msg}")
    {
        WorkflowId = workflowId;
    }
}
