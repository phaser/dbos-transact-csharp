using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class WorkflowOutputsResponse : BaseResponse
{
    [JsonPropertyName("output")]
    public List<WorkflowsOutput> Output { get; set; } = [];

    public WorkflowOutputsResponse() { }

    public WorkflowOutputsResponse(BaseMessage message, List<WorkflowsOutput> output)
        : base(message.Type, message.RequestId) => Output = output;

    public WorkflowOutputsResponse(BaseMessage message, Exception ex)
        : base(message.Type, message.RequestId, ex.Message) { }
}
