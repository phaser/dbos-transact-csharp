using System.Globalization;
using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

public sealed class GetMetricsRequest : BaseMessage
{
    [JsonPropertyName("start_time")]
    public string? StartTime { get; set; }

    [JsonPropertyName("end_time")]
    public string? EndTime { get; set; }

    [JsonPropertyName("metric_class")]
    public string? MetricClass { get; set; }

    public GetMetricsRequest() { Type = MessageType.GetMetrics.GetValue(); }

    public GetMetricsRequest(string requestId, DateTimeOffset? startTime, DateTimeOffset? endTime, string? metricClass)
    {
        Type = MessageType.GetMetrics.GetValue();
        RequestId = requestId;
        StartTime = startTime?.ToString("O");
        EndTime = endTime?.ToString("O");
        MetricClass = metricClass;
    }

    public DateTimeOffset? ParsedStartTime =>
        StartTime is null ? null : DateTimeOffset.Parse(StartTime, CultureInfo.InvariantCulture);

    public DateTimeOffset? ParsedEndTime =>
        EndTime is null ? null : DateTimeOffset.Parse(EndTime, CultureInfo.InvariantCulture);
}
