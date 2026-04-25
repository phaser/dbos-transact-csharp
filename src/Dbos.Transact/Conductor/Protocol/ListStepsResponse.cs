using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class ListStepsResponse : BaseResponse
{
    public sealed class StepEntry
    {
        [JsonPropertyName("function_id")] public int FunctionId { get; set; }
        [JsonPropertyName("function_name")] public string? FunctionName { get; set; }
        [JsonPropertyName("output")] public string? Output { get; set; }
        [JsonPropertyName("error")] public string? Error { get; set; }
        [JsonPropertyName("child_workflow_id")] public string? ChildWorkflowId { get; set; }
        [JsonPropertyName("started_at_epoch_ms")] public string? StartedAtEpochMs { get; set; }
        [JsonPropertyName("completed_at_epoch_ms")] public string? CompletedAtEpochMs { get; set; }

        public StepEntry() { }

        public StepEntry(StepInfo info)
        {
            FunctionId = info.FunctionId;
            FunctionName = info.FunctionName;
            Output = info.Output is not null ? JsonSerializer.Serialize(info.Output) : null;
            Error = info.Error is not null
                ? $"{info.Error.ClassName}: {info.Error.Message}"
                : null;
            ChildWorkflowId = info.ChildWorkflowId;
            StartedAtEpochMs = info.StartedAtEpochMs?.ToString(CultureInfo.InvariantCulture);
            CompletedAtEpochMs = info.CompletedAtEpochMs?.ToString(CultureInfo.InvariantCulture);
        }
    }

    [JsonPropertyName("output")]
    public List<StepEntry> Output { get; set; } = [];

    public ListStepsResponse() { }

    public ListStepsResponse(BaseMessage message, List<StepEntry> output)
        : base(message.Type, message.RequestId) => Output = output;

    public ListStepsResponse(BaseMessage message, Exception ex)
        : base(message.Type, message.RequestId, ex.Message) { }
}
