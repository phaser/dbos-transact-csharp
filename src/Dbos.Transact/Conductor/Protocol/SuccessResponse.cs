using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class SuccessResponse : BaseResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    public SuccessResponse() { }

    public SuccessResponse(BaseMessage message, bool success)
        : base(message.Type, message.RequestId) => Success = success;

    public SuccessResponse(BaseMessage message, Exception ex)
        : base(message.Type, message.RequestId, ex.Message) { }
}
