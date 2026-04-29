namespace Dbos.Transact.Database;

/// <summary>
/// A piece of state associated with an external service such as an event dispatcher.
/// Models a key/value entry stored by the DBOS system database on behalf of an external
/// service. Includes identifying information (service name, fully qualified workflow name,
/// key within), a value, and optional metadata for versioning the update.
/// Port of Java <c>ExternalState</c>.
/// </summary>
/// <param name="Service">The name of the external service that owns or stores the state.</param>
/// <param name="WorkflowName">The fully qualified function name of the workflow this state belongs to.</param>
/// <param name="Key">The key under which the state is stored, allowing multiple values per service/workflow combination.</param>
/// <param name="Value">The current value associated with the key.</param>
/// <param name="UpdateTime">Timestamp of the last update, expressed as decimal seconds since the Unix epoch (or <c>null</c> if unused).</param>
/// <param name="UpdateSeq">Monotonic sequence number for updates, used to detect the latest version (or <c>null</c> if not applicable).</param>
public sealed record ExternalState(
    string Service,
    string WorkflowName,
    string Key,
    string? Value,
    decimal? UpdateTime,
    long? UpdateSeq)
{
    public string Service { get; init; } = string.IsNullOrEmpty(Service)
        ? throw new ArgumentException("Service must not be null or empty.", nameof(Service))
        : Service;

    public string WorkflowName { get; init; } = string.IsNullOrEmpty(WorkflowName)
        ? throw new ArgumentException("WorkflowName must not be null or empty.", nameof(WorkflowName))
        : WorkflowName;

    public string Key { get; init; } = string.IsNullOrEmpty(Key)
        ? throw new ArgumentException("Key must not be null or empty.", nameof(Key))
        : Key;

    public ExternalState(string service, string workflowName, string key)
        : this(service, workflowName, key, null, null, null)
    {
    }
}
