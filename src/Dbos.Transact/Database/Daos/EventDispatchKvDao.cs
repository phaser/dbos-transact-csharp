namespace Dbos.Transact.Database.Daos;

/// <summary>
/// Data-access methods for the <c>event_dispatch_kv</c> table — durable key/value
/// storage for external services (e.g. <c>SchedulerService</c> last-fire tracking).
/// Port of Java's <c>SystemDatabase.getExternalState</c> / <c>upsertExternalState</c>.
/// </summary>
public abstract class EventDispatchKvDao
{
    public abstract Task<ExternalState?> GetExternalStateAsync(string service, string workflowName, string key, CancellationToken ct = default);

    /// <summary>
    /// Upserts the row identified by <c>(Service, WorkflowName, Key)</c>. The existing row's
    /// <c>UpdateTime</c> / <c>UpdateSeq</c> always advance to the maximum of incoming and existing,
    /// and <c>Value</c> is overwritten only when the incoming row is strictly newer (by either time
    /// or seq) or when the existing row has neither. Returns the post-upsert state.
    /// </summary>
    public abstract Task<ExternalState> UpsertExternalStateAsync(ExternalState state, CancellationToken ct = default);
}
