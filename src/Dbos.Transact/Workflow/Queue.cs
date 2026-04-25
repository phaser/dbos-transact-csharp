namespace Dbos.Transact.Workflow;

/// <summary>
/// Rate limit configuration for a DBOS queue.
/// </summary>
public sealed record RateLimit(int Limit, TimeSpan Period);

/// <summary>
/// Definition of a DBOS workflow queue with optional concurrency, priority, partitioning, and rate-limiting.
/// </summary>
#pragma warning disable CA1711 // name mirrors the Java API; renaming would break port fidelity
public sealed record Queue(
    string Name,
    int? Concurrency,
    int? WorkerConcurrency,
    bool PriorityEnabled,
    bool PartitioningEnabled,
    RateLimit? RateLimit)
{
    public string Name { get; init; } = string.IsNullOrEmpty(Name)
        ? throw new ArgumentException("Queue name must not be null or empty.", nameof(Name))
        : Name;

    public int? Concurrency { get; init; } = Concurrency is <= 0
        ? throw new ArgumentOutOfRangeException(nameof(Concurrency), "Queue concurrency must be greater than zero.")
        : Concurrency;

    public int? WorkerConcurrency { get; init; } = WorkerConcurrency is <= 0
        ? throw new ArgumentOutOfRangeException(nameof(WorkerConcurrency), "Queue workerConcurrency must be greater than zero.")
        : WorkerConcurrency;

    public Queue(string name) : this(name, null, null, false, false, null) { }

    public bool HasLimiter => RateLimit is not null;
}
#pragma warning restore CA1711
