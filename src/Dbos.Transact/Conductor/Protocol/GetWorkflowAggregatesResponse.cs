using System.Text.Json.Serialization;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class GetWorkflowAggregatesResponse : BaseResponse
{
    public sealed record WorkflowAggregateOutput(
        [property: JsonPropertyName("group")] IReadOnlyDictionary<string, string?> Group,
        [property: JsonPropertyName("count")] long Count)
    {
        public static WorkflowAggregateOutput From(WorkflowAggregateRow row) =>
            new(row.Group, row.Count);
    }

    [JsonPropertyName("output")]
    public List<WorkflowAggregateOutput> Output { get; set; } = [];

    public GetWorkflowAggregatesResponse() { }

    public GetWorkflowAggregatesResponse(BaseMessage message, List<WorkflowAggregateRow> rows)
        : base(message.Type, message.RequestId) =>
        Output = rows.ConvertAll(WorkflowAggregateOutput.From);

    public GetWorkflowAggregatesResponse(BaseMessage message, Exception ex)
        : base(message.Type, message.RequestId, ex.Message) { }
}
