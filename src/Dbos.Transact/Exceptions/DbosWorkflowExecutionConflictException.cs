namespace Dbos.Transact.Exceptions;

/// <summary>
/// Thrown when DBOS detects two concurrent executions running under the same workflow ID.
/// All but one of the concurrent executions will be terminated by throwing this exception.
/// This exception should not be caught or otherwise handled.
/// </summary>
public sealed class DbosWorkflowExecutionConflictException : DbosException
{
    public string WorkflowId { get; }

    public DbosWorkflowExecutionConflictException(string workflowId)
        : base($"Conflicting workflow ID {workflowId}")
    {
        WorkflowId = workflowId;
    }
}
