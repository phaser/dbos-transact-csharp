using System.Text.Json;
using System.Text.Json.Serialization;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Admin;

internal sealed record StepOutput(
    [property: JsonPropertyName("function_id")] int FunctionId,
    [property: JsonPropertyName("function_name")] string? FunctionName,
    [property: JsonPropertyName("output")] string? Output,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("child_workflow_id")] string? ChildWorkflowId)
{
    internal static StepOutput Of(StepInfo info) => new(
        info.FunctionId,
        info.FunctionName,
        info.Output is null ? null : JsonSerializer.Serialize(info.Output),
        info.Error is null ? null : JsonSerializer.Serialize(info.Error),
        info.ChildWorkflowId);
}
