namespace Dbos.Transact.Workflow;

/// <summary>
/// Discriminated union representing timeout behaviour for a workflow.
/// Mirrors Java's sealed Timeout interface.
/// </summary>
public abstract record Timeout
{
    /// <summary>Inherit the timeout from the parent context.</summary>
    public sealed record Inherit : Timeout;

    /// <summary>Explicitly disable any timeout.</summary>
    public sealed record None : Timeout;

    /// <summary>An explicit positive timeout duration.</summary>
    public sealed record Explicit(TimeSpan Value) : Timeout
    {
        public TimeSpan Value { get; init; } = Value <= TimeSpan.Zero
            ? throw new ArgumentOutOfRangeException(nameof(Value), "Explicit timeout must be a positive non-zero duration.")
            : Value;
    }

    public static Timeout InheritTimeout() => new Inherit();
    public static Timeout NoTimeout() => new None();
    public static Timeout Of(TimeSpan d) => new Explicit(d);
    public static Timeout Of(long milliseconds) => new Explicit(TimeSpan.FromMilliseconds(milliseconds));
}
