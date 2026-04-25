using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public class BaseResponse
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }

    [JsonPropertyName("error_message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorMessage { get; set; }

    public BaseResponse() { }

    public BaseResponse(string? type, string? requestId)
    {
        Type = type;
        RequestId = requestId;
    }

    public BaseResponse(string? type, string? requestId, string? errorMessage)
    {
        Type = type;
        RequestId = requestId;
        ErrorMessage = errorMessage;
    }
}
