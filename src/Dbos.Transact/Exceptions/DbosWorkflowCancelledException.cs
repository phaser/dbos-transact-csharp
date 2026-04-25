namespace Dbos.Transact.Exceptions;

/// <summary>
/// Thrown when DBOS determines that a workflow has exceeded its timeout and should be terminated.
/// Exception handling (if any) should take no further durable actions.
/// </summary>
public sealed class DbosWorkflowCancelledException : DbosException
{
    public string WorkflowId { get; }

    public DbosWorkflowCancelledException(string workflowId)
        : base($"Workflow {workflowId} has been cancelled")
    {
        WorkflowId = workflowId;
    }
}
