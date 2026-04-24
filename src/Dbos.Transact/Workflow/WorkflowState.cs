namespace Dbos.Transact.Workflow;

public enum WorkflowState
{
    Pending = 0,
    Success = 1,
    Error = 2,
    MaxRecoveryAttemptsExceeded = 3,
    Cancelled = 4,
    Enqueued = 5,
    Delayed = 6,
}

public static class WorkflowStateExtensions
{
    public static bool IsActive(this WorkflowState state) =>
        state is WorkflowState.Pending or WorkflowState.Enqueued or WorkflowState.Delayed;
}
