using System.Text.Json.Serialization;
using Dbos.Transact.Database;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class GetMetricsResponse : BaseResponse
{
    public sealed record MetricsDataOutput(
        [property: JsonPropertyName("metric_type")] string MetricType,
        [property: JsonPropertyName("metric_name")] string MetricName,
        [property: JsonPropertyName("value")] long Value)
    {
        public static MetricsDataOutput FromMetricData(MetricData m) =>
            new(m.MetricType, m.MetricName, m.Value);
    }

    [JsonPropertyName("metrics")]
    public List<MetricsDataOutput> Metrics { get; set; } = [];

    public GetMetricsResponse() { }

    public GetMetricsResponse(BaseMessage message, List<MetricData> metrics)
        : base(message.Type, message.RequestId) =>
        Metrics = metrics.ConvertAll(MetricsDataOutput.FromMetricData);

    public GetMetricsResponse(BaseMessage message, Exception ex)
        : base(message.Type, message.RequestId, ex.Message) { }
}
