namespace Dbos.Transact.Workflow.Internal;

/// <summary>
/// Durable record of a single step execution. Port of Java's <c>StepResult</c> record.
/// </summary>
public sealed record StepResult(
    string WorkflowId,
    int StepId,
    string FunctionName,
    string? Output = null,
    string? Error = null,
    string? ChildWorkflowId = null,
    string? Serialization = null)
{
    public StepResult WithOutput(string? output) =>
        this with { Output = output };

    public StepResult WithError(string? error) =>
        this with { Error = error };

    public StepResult WithChildWorkflowId(string? childWorkflowId) =>
        this with { ChildWorkflowId = childWorkflowId };

    public StepResult WithSerialization(string? serialization) =>
        this with { Serialization = serialization };
}
