using System.Text.Json;
using System.Text.Json.Serialization;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Conductor.Protocol;

public sealed record ScheduleOutput(
    [property: JsonPropertyName("schedule_id")] string? ScheduleId,
    [property: JsonPropertyName("schedule_name")] string? ScheduleName,
    [property: JsonPropertyName("workflow_name")] string? WorkflowName,
    [property: JsonPropertyName("workflow_class_name")] string? WorkflowClassName,
    [property: JsonPropertyName("schedule")] string? Schedule,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("context")] string? Context,
    [property: JsonPropertyName("last_fired_at")] string? LastFiredAt,
    [property: JsonPropertyName("automatic_backfill")] bool AutomaticBackfill,
    [property: JsonPropertyName("cron_timezone")] string? CronTimezone,
    [property: JsonPropertyName("queue_name")] string? QueueName)
{
    public static ScheduleOutput From(WorkflowSchedule s, bool loadContext) => new(
        s.Id,
        s.ScheduleName,
        s.WorkflowName,
        s.ClassName,
        s.Cron,
        s.Status.ToString(),
        loadContext && s.Context is not null ? JsonSerializer.Serialize(s.Context) : null,
        s.LastFiredAt?.ToString("O"),
        s.AutomaticBackfill,
        s.CronTimezone?.Id,
        s.QueueName);
}
