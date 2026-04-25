using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Admin;

internal sealed record WorkflowsOutput(
    [property: JsonPropertyName("WorkflowUUID")] string? WorkflowUuid,
    [property: JsonPropertyName("Status")] string? Status,
    [property: JsonPropertyName("WorkflowName")] string? WorkflowName,
    [property: JsonPropertyName("WorkflowClassName")] string? WorkflowClassName,
    [property: JsonPropertyName("WorkflowConfigName")] string? WorkflowConfigName,
    [property: JsonPropertyName("AuthenticatedUser")] string? AuthenticatedUser,
    [property: JsonPropertyName("AssumedRole")] string? AssumedRole,
    [property: JsonPropertyName("AuthenticatedRoles")] string? AuthenticatedRoles,
    [property: JsonPropertyName("Input")] string? Input,
    [property: JsonPropertyName("Output")] string? Output,
    [property: JsonPropertyName("Request")] string? Request,
    [property: JsonPropertyName("Error")] string? Error,
    [property: JsonPropertyName("CreatedAt")] string? CreatedAt,
    [property: JsonPropertyName("UpdatedAt")] string? UpdatedAt,
    [property: JsonPropertyName("QueueName")] string? QueueName,
    [property: JsonPropertyName("ApplicationVersion")] string? ApplicationVersion,
    [property: JsonPropertyName("ExecutorID")] string? ExecutorId)
{
    internal static WorkflowsOutput Of(WorkflowStatus status)
    {
        var roles = status.AuthenticatedRoles is null
            ? "[]"
            : JsonSerializer.Serialize(status.AuthenticatedRoles);
        var input = status.Input is null ? "[]" : JsonSerializer.Serialize(status.Input);
        var output = status.Output is null ? null : JsonSerializer.Serialize(status.Output);
        var error = status.Error is null ? null : JsonSerializer.Serialize(status.Error);

        return new WorkflowsOutput(
            status.WorkflowId,
            status.Status?.ToString(),
            status.WorkflowName,
            status.ClassName,
            status.InstanceName,
            status.AuthenticatedUser,
            status.AssumedRole,
            roles,
            input,
            output,
            null,
            error,
            status.CreatedAtEpochMs?.ToString(CultureInfo.InvariantCulture),
            status.UpdatedAtEpochMs?.ToString(CultureInfo.InvariantCulture),
            status.QueueName,
            status.AppVersion,
            status.ExecutorId);
    }
}
