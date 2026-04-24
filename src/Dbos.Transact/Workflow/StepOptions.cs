namespace Dbos.Transact.Workflow;

public sealed record StepOptions(
    string Name,
    int MaxAttempts,
    TimeSpan RetryInterval,
    double BackOffRate)
{
    public const double DefaultIntervalSeconds = 1.0;
    public const double DefaultBackOff = 2.0;

    public int MaxAttempts { get; init; } = MaxAttempts < 1 ? 1 : MaxAttempts;

    public StepOptions(string name)
        : this(name, 1, TimeSpan.FromSeconds(DefaultIntervalSeconds), DefaultBackOff)
    {
    }

    // Factory method from [Step] attribute + MethodInfo reflection is deferred to the interception wave.
}
