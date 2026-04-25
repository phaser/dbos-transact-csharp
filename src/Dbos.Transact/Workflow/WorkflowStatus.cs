namespace Dbos.Transact.Workflow;

/// <summary>
/// Status and metadata snapshot of a workflow execution.
/// Array fields (AuthenticatedRoles, Input) require custom equality — implemented here.
/// </summary>
public sealed record WorkflowStatus(
    string? WorkflowId,
    WorkflowState? Status,
    string? WorkflowName,
    string? ClassName,
    string? InstanceName,
    string? AuthenticatedUser,
    string? AssumedRole,
    string[]? AuthenticatedRoles,
    object?[]? Input,
    object? Output,
    ErrorResult? Error,
    string? ExecutorId,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? AppVersion,
    string? AppId,
    int? RecoveryAttempts,
    string? QueueName,
    TimeSpan? Timeout,
    DateTimeOffset? Deadline,
    DateTimeOffset? StartedAt,
    string? DeduplicationId,
    int? Priority,
    string? QueuePartitionKey,
    string? ForkedFrom,
    string? ParentWorkflowId,
    bool? WasForkedFrom,
    DateTimeOffset? DelayUntil,
    string? Serialization)
{
    public long? TimeoutMs => Timeout?.Ticks / TimeSpan.TicksPerMillisecond;
    public long? DeadlineEpochMs => Deadline?.ToUnixTimeMilliseconds();
    public long? CreatedAtEpochMs => CreatedAt?.ToUnixTimeMilliseconds();
    public long? UpdatedAtEpochMs => UpdatedAt?.ToUnixTimeMilliseconds();
    public long? StartedAtEpochMs => StartedAt?.ToUnixTimeMilliseconds();
    public long? DelayUntilEpochMs => DelayUntil?.ToUnixTimeMilliseconds();

    public bool Equals(WorkflowStatus? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return WorkflowId == other.WorkflowId
            && Status == other.Status
            && WorkflowName == other.WorkflowName
            && ClassName == other.ClassName
            && InstanceName == other.InstanceName
            && AuthenticatedUser == other.AuthenticatedUser
            && AssumedRole == other.AssumedRole
            && (AuthenticatedRoles == other.AuthenticatedRoles
                || (AuthenticatedRoles is not null && other.AuthenticatedRoles is not null
                    && AuthenticatedRoles.SequenceEqual(other.AuthenticatedRoles)))
            && (Input == other.Input
                || (Input is not null && other.Input is not null
                    && Input.SequenceEqual(other.Input)))
            && Equals(Output, other.Output)
            && Equals(Error, other.Error)
            && ExecutorId == other.ExecutorId
            && CreatedAt == other.CreatedAt
            && UpdatedAt == other.UpdatedAt
            && AppVersion == other.AppVersion
            && AppId == other.AppId
            && RecoveryAttempts == other.RecoveryAttempts
            && QueueName == other.QueueName
            && Timeout == other.Timeout
            && Deadline == other.Deadline
            && StartedAt == other.StartedAt
            && DeduplicationId == other.DeduplicationId
            && Priority == other.Priority
            && QueuePartitionKey == other.QueuePartitionKey
            && ForkedFrom == other.ForkedFrom
            && ParentWorkflowId == other.ParentWorkflowId
            && WasForkedFrom == other.WasForkedFrom
            && DelayUntil == other.DelayUntil;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(WorkflowId);
        hash.Add(Status);
        hash.Add(WorkflowName);
        hash.Add(ClassName);
        hash.Add(InstanceName);
        hash.Add(AuthenticatedUser);
        hash.Add(AssumedRole);
        if (AuthenticatedRoles is not null)
            foreach (var r in AuthenticatedRoles) hash.Add(r);
        if (Input is not null)
            foreach (var a in Input) hash.Add(a);
        hash.Add(Output);
        hash.Add(Error);
        hash.Add(ExecutorId);
        hash.Add(CreatedAt);
        hash.Add(UpdatedAt);
        hash.Add(AppVersion);
        hash.Add(AppId);
        hash.Add(RecoveryAttempts);
        hash.Add(QueueName);
        hash.Add(Timeout);
        hash.Add(Deadline);
        hash.Add(StartedAt);
        hash.Add(DeduplicationId);
        hash.Add(Priority);
        hash.Add(QueuePartitionKey);
        hash.Add(ForkedFrom);
        hash.Add(ParentWorkflowId);
        hash.Add(WasForkedFrom);
        hash.Add(DelayUntil);
        return hash.ToHashCode();
    }
}
