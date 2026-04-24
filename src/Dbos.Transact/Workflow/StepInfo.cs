namespace Dbos.Transact.Workflow;

public sealed record StepInfo(
    int FunctionId,
    string? FunctionName,
    object? Output,
    ErrorResult? Error,
    string? ChildWorkflowId,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Serialization)
{
    public long? StartedAtEpochMs => StartedAt?.ToUnixTimeMilliseconds();
    public long? CompletedAtEpochMs => CompletedAt?.ToUnixTimeMilliseconds();
}
