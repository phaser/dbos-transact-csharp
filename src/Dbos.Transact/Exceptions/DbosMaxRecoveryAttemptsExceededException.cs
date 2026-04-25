namespace Dbos.Transact.Exceptions;

/// <summary>
/// Thrown when workflow recovery is attempted but the maximum number of recovery attempts
/// have already been made.
/// </summary>
public sealed class DbosMaxRecoveryAttemptsExceededException : DbosException
{
    public string WorkflowId { get; }
    public int MaxRetries { get; }

    public DbosMaxRecoveryAttemptsExceededException(string workflowId, int maxRetries)
        : base($"Workflow {workflowId} has been moved to the dead-letter queue after exceeding the maximum of {maxRetries} retries")
    {
        WorkflowId = workflowId;
        MaxRetries = maxRetries;
    }
}
