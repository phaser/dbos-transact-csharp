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

    /// <summary>Produces a new queue with the assigned name.</summary>
    public Queue WithName(string name) => this with { Name = name };

    /// <summary>Produces a new queue with the assigned global concurrency. Pass <c>null</c> to remove the limit.</summary>
    public Queue WithConcurrency(int? concurrency) => this with { Concurrency = concurrency };

    /// <summary>Produces a new queue with the assigned per-worker concurrency. Pass <c>null</c> to remove the limit.</summary>
    public Queue WithWorkerConcurrency(int? workerConcurrency) => this with { WorkerConcurrency = workerConcurrency };

    /// <summary>Produces a new queue with prioritization enabled or disabled.</summary>
    public Queue WithPriorityEnabled(bool priorityEnabled) => this with { PriorityEnabled = priorityEnabled };

    /// <summary>Produces a new queue with partitioning enabled or disabled.</summary>
    public Queue WithPartitioningEnabled(bool partitioningEnabled) => this with { PartitioningEnabled = partitioningEnabled };

    /// <summary>Produces a new queue with the assigned rate limit. Pass <c>null</c> to remove the limit.</summary>
    public Queue WithRateLimit(RateLimit? rateLimit) => this with { RateLimit = rateLimit };

    /// <summary>Produces a new queue with the assigned rate limit, expressed in workflows per period duration.</summary>
    public Queue WithRateLimit(int limit, TimeSpan period) => WithRateLimit(new RateLimit(limit, period));

    /// <summary>Produces a new queue with the assigned rate limit, expressed in workflows per period (seconds).</summary>
    public Queue WithRateLimit(int limit, double periodSeconds) =>
        WithRateLimit(new RateLimit(limit, TimeSpan.FromMilliseconds(periodSeconds * 1000)));
}
#pragma warning restore CA1711
