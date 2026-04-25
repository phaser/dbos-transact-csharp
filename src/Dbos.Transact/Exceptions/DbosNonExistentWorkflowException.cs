namespace Dbos.Transact.Exceptions;

/// <summary>
/// Thrown by DBOS functions such as <c>Send</c> or <c>ExecuteWorkflowById</c> that require a
/// workflow to exist prior to invocation.
/// </summary>
public sealed class DbosNonExistentWorkflowException : DbosException
{
    public string WorkflowId { get; }

    public DbosNonExistentWorkflowException(string workflowId)
        : base($"Workflow does not exist {workflowId}")
    {
        WorkflowId = workflowId;
    }
}
