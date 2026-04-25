using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class RetentionRequest : BaseMessage
{
    [JsonPropertyName("body")]
    public RetentionBody? Body { get; set; }

    public RetentionRequest() { Type = MessageType.Retention.GetValue(); }

    public RetentionRequest(string requestId, long? gcCutoff, long? gcRowsThreshold, long? timeoutCutoff)
    {
        Type = MessageType.Retention.GetValue();
        RequestId = requestId;
        Body = new RetentionBody
        {
            GcCutoffEpochMs = gcCutoff,
            GcRowsThreshold = gcRowsThreshold,
            TimeoutCutoffEpochMs = timeoutCutoff,
        };
    }

    public sealed class RetentionBody
    {
        [JsonPropertyName("gc_cutoff_epoch_ms")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? GcCutoffEpochMs { get; set; }

        [JsonPropertyName("gc_rows_threshold")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? GcRowsThreshold { get; set; }

        [JsonPropertyName("timeout_cutoff_epoch_ms")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? TimeoutCutoffEpochMs { get; set; }
    }
}
