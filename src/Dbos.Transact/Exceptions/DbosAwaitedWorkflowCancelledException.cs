namespace Dbos.Transact.Exceptions;

/// <summary>
/// Thrown when a call requests the return value of a workflow that was cancelled.
/// Calls such as <c>GetResult</c> and <c>WorkflowHandle.GetResult</c> may throw this exception.
/// </summary>
public sealed class DbosAwaitedWorkflowCancelledException : DbosException
{
    public string WorkflowId { get; }

    public DbosAwaitedWorkflowCancelledException(string workflowId)
        : base($"Awaited workflow {workflowId} was cancelled.")
    {
        WorkflowId = workflowId;
    }
}
