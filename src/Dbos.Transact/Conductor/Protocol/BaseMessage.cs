using System.Text.Json.Serialization;

namespace Dbos.Transact.Conductor.Protocol;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type", IgnoreUnrecognizedTypeDiscriminators = true)]
[JsonDerivedType(typeof(AlertRequest), "alert")]
[JsonDerivedType(typeof(BackfillScheduleRequest), "backfill_schedule")]
[JsonDerivedType(typeof(CancelRequest), "cancel")]
[JsonDerivedType(typeof(DeleteRequest), "delete")]
[JsonDerivedType(typeof(ExecutorInfoRequest), "executor_info")]
[JsonDerivedType(typeof(ExistPendingWorkflowsRequest), "exist_pending_workflows")]
[JsonDerivedType(typeof(ExportWorkflowRequest), "export_workflow")]
[JsonDerivedType(typeof(ForkWorkflowRequest), "fork_workflow")]
[JsonDerivedType(typeof(GetMetricsRequest), "get_metrics")]
[JsonDerivedType(typeof(GetScheduleRequest), "get_schedule")]
[JsonDerivedType(typeof(GetWorkflowAggregatesRequest), "get_workflow_aggregates")]
[JsonDerivedType(typeof(GetWorkflowEventsRequest), "get_workflow_events")]
[JsonDerivedType(typeof(GetWorkflowNotificationsRequest), "get_workflow_notifications")]
[JsonDerivedType(typeof(GetWorkflowStreamsRequest), "get_workflow_streams")]
[JsonDerivedType(typeof(GetWorkflowRequest), "get_workflow")]
[JsonDerivedType(typeof(ImportWorkflowRequest), "import_workflow")]
[JsonDerivedType(typeof(ListApplicationVersionsRequest), "list_application_versions")]
[JsonDerivedType(typeof(ListQueuedWorkflowsRequest), "list_queued_workflows")]
[JsonDerivedType(typeof(ListSchedulesRequest), "list_schedules")]
[JsonDerivedType(typeof(ListStepsRequest), "list_steps")]
[JsonDerivedType(typeof(ListWorkflowsRequest), "list_workflows")]
[JsonDerivedType(typeof(PauseScheduleRequest), "pause_schedule")]
[JsonDerivedType(typeof(RecoveryRequest), "recovery")]
[JsonDerivedType(typeof(RestartRequest), "restart")]
[JsonDerivedType(typeof(ResumeRequest), "resume")]
[JsonDerivedType(typeof(ResumeScheduleRequest), "resume_schedule")]
[JsonDerivedType(typeof(RetentionRequest), "retention")]
[JsonDerivedType(typeof(SetLatestApplicationVersionRequest), "set_latest_application_version")]
[JsonDerivedType(typeof(TriggerScheduleRequest), "trigger_schedule")]
public abstract class BaseMessage
{
    // Not serialized as a JSON property — the polymorphic discriminator writes "type" to JSON.
    // Set in each concrete constructor for programmatic access (e.g. to populate response Type).
    [JsonIgnore]
    public string? Type { get; protected set; }

    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }
}
