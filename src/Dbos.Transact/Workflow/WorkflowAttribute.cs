namespace Dbos.Transact.Workflow;

/// <summary>
/// Marks a method as a DBOS durable workflow entry point.
/// Port of Java <c>@Workflow</c> annotation.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class WorkflowAttribute : Attribute
{
    /// <summary>Optional override name for workflow registration. Defaults to the method name.</summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Maximum number of recovery attempts before the workflow is moved to the dead-letter queue.
    /// -1 means use the executor default (<see cref="Constants.DefaultMaxRecoveryAttempts"/>).
    /// </summary>
    public int MaxRecoveryAttempts { get; set; } = -1;

    /// <summary>Serialization strategy for this workflow's arguments and result.</summary>
    public SerializationStrategy SerializationStrategy { get; set; } = SerializationStrategy.Default;
}
