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

    /// <summary>Converts the enum value to the UPPERCASE_UNDERSCORE string stored in the database.</summary>
    public static string ToDbString(this WorkflowState state) => state switch
    {
        WorkflowState.Pending => "PENDING",
        WorkflowState.Success => "SUCCESS",
        WorkflowState.Error => "ERROR",
        WorkflowState.MaxRecoveryAttemptsExceeded => "MAX_RECOVERY_ATTEMPTS_EXCEEDED",
        WorkflowState.Cancelled => "CANCELLED",
        WorkflowState.Enqueued => "ENQUEUED",
        WorkflowState.Delayed => "DELAYED",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    /// <summary>Parses a database status string (e.g. "PENDING") into <see cref="WorkflowState"/>.</summary>
    public static WorkflowState ParseDbStatus(string status) => status switch
    {
        "PENDING" => WorkflowState.Pending,
        "SUCCESS" => WorkflowState.Success,
        "ERROR" => WorkflowState.Error,
        "MAX_RECOVERY_ATTEMPTS_EXCEEDED" => WorkflowState.MaxRecoveryAttemptsExceeded,
        "CANCELLED" => WorkflowState.Cancelled,
        "ENQUEUED" => WorkflowState.Enqueued,
        "DELAYED" => WorkflowState.Delayed,
        _ => throw new ArgumentException($"Unknown workflow status: '{status}'", nameof(status)),
    };
}
