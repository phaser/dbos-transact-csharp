namespace Dbos.Transact.Exceptions;

/// <summary>
/// Thrown when a replayed workflow attempts to execute a different step than it did during its
/// original run, indicating that the workflow function is not deterministic.
/// </summary>
public sealed class DbosUnexpectedStepException : DbosException
{
    public string WorkflowId { get; }
    public int StepId { get; }
    public string AttemptedName { get; }
    public string RecordedName { get; }

    public DbosUnexpectedStepException(string workflowId, int stepId, string attemptedName, string recordedName)
        : base($"During reexecution of workflow {workflowId} step {stepId}, function {attemptedName} was attempted but {recordedName} was executed in the original execution. Check that your workflow is deterministic.")
    {
        WorkflowId = workflowId;
        StepId = stepId;
        AttemptedName = attemptedName;
        RecordedName = recordedName;
    }
}
