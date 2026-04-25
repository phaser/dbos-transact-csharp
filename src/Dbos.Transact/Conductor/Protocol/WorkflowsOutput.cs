using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class WorkflowsOutput
{
    [JsonPropertyName("WorkflowUUID")] public string? WorkflowUuid { get; set; }
    [JsonPropertyName("Status")] public string? Status { get; set; }
    [JsonPropertyName("WorkflowName")] public string? WorkflowName { get; set; }
    [JsonPropertyName("WorkflowClassName")] public string? WorkflowClassName { get; set; }
    [JsonPropertyName("WorkflowConfigName")] public string? WorkflowConfigName { get; set; }
    [JsonPropertyName("AuthenticatedUser")] public string? AuthenticatedUser { get; set; }
    [JsonPropertyName("AssumedRole")] public string? AssumedRole { get; set; }
    [JsonPropertyName("AuthenticatedRoles")] public string? AuthenticatedRoles { get; set; }
    [JsonPropertyName("Input")] public string? Input { get; set; }
    [JsonPropertyName("Output")] public string? Output { get; set; }
    [JsonPropertyName("Request")] public string? Request { get; set; }
    [JsonPropertyName("Error")] public string? Error { get; set; }
    [JsonPropertyName("CreatedAt")] public string? CreatedAt { get; set; }
    [JsonPropertyName("UpdatedAt")] public string? UpdatedAt { get; set; }
    [JsonPropertyName("QueueName")] public string? QueueName { get; set; }
    [JsonPropertyName("ApplicationVersion")] public string? ApplicationVersion { get; set; }
    [JsonPropertyName("ExecutorID")] public string? ExecutorId { get; set; }
    [JsonPropertyName("WorkflowTimeoutMS")] public string? WorkflowTimeoutMs { get; set; }
    [JsonPropertyName("WorkflowDeadlineEpochMS")] public string? WorkflowDeadlineEpochMs { get; set; }
    [JsonPropertyName("DeduplicationID")] public string? DeduplicationId { get; set; }
    [JsonPropertyName("Priority")] public string? Priority { get; set; }
    [JsonPropertyName("QueuePartitionKey")] public string? QueuePartitionKey { get; set; }
    [JsonPropertyName("ForkedFrom")] public string? ForkedFrom { get; set; }
    [JsonPropertyName("ParentWorkflowID")] public string? ParentWorkflowId { get; set; }
    [JsonPropertyName("DequeuedAt")] public string? DequeuedAt { get; set; }
    [JsonPropertyName("WasForkedFrom")] public bool? WasForkedFrom { get; set; }
    [JsonPropertyName("DelayUntilEpochMS")] public string? DelayUntilEpochMs { get; set; }

    public WorkflowsOutput() { }

    public WorkflowsOutput(WorkflowStatus status)
    {
        var roles = status.AuthenticatedRoles is { Length: > 0 }
            ? JsonSerializer.Serialize(status.AuthenticatedRoles)
            : null;

        WorkflowUuid = status.WorkflowId;
        Status = status.Status?.ToString();
        WorkflowName = status.WorkflowName;
        WorkflowClassName = status.ClassName;
        WorkflowConfigName = status.InstanceName;
        AuthenticatedUser = status.AuthenticatedUser;
        AssumedRole = status.AssumedRole;
        AuthenticatedRoles = roles;
        Input = status.Input is not null ? JsonSerializer.Serialize(status.Input) : null;
        Output = status.Output is not null ? JsonSerializer.Serialize(status.Output) : null;
        Request = null;
        Error = status.Error is not null
            ? $"{status.Error.ClassName}: {status.Error.Message}"
            : null;
        CreatedAt = status.CreatedAtEpochMs?.ToString(CultureInfo.InvariantCulture);
        UpdatedAt = status.UpdatedAtEpochMs?.ToString(CultureInfo.InvariantCulture);
        QueueName = status.QueueName;
        ApplicationVersion = status.AppVersion;
        ExecutorId = status.ExecutorId;
        WorkflowTimeoutMs = status.TimeoutMs?.ToString(CultureInfo.InvariantCulture);
        WorkflowDeadlineEpochMs = status.DeadlineEpochMs?.ToString(CultureInfo.InvariantCulture);
        DeduplicationId = status.DeduplicationId;
        Priority = (status.Priority ?? 0).ToString(CultureInfo.InvariantCulture);
        QueuePartitionKey = status.QueuePartitionKey;
        ForkedFrom = status.ForkedFrom;
        ParentWorkflowId = status.ParentWorkflowId;
        DequeuedAt = status.StartedAtEpochMs?.ToString(CultureInfo.InvariantCulture);
        WasForkedFrom = status.WasForkedFrom;
        DelayUntilEpochMs = status.DelayUntilEpochMs?.ToString(CultureInfo.InvariantCulture);
    }
}
