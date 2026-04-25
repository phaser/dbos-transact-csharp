namespace Dbos.Transact.Workflow;

/// <summary>
/// Marks a method as a DBOS durable step within a workflow.
/// Port of Java <c>@Step</c> annotation.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class StepAttribute : Attribute
{
    /// <summary>Optional override name for step registration. Defaults to the method name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Whether retries are allowed on failure.</summary>
    public bool RetriesAllowed { get; set; }

    /// <summary>Initial retry interval in seconds.</summary>
    public double IntervalSeconds { get; set; } = StepOptions.DefaultIntervalSeconds;

    /// <summary>Maximum number of attempts (including the first).</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Exponential back-off multiplier applied between retries.</summary>
    public double BackOffRate { get; set; } = StepOptions.DefaultBackOff;
}
