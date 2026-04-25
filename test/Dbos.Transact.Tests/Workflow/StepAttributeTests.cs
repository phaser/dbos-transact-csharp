using System.Reflection;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Workflow;

public class StepAttributeTests
{
    [Step]
    private static void DefaultStep() { }

    [Step(Name = "s", RetriesAllowed = true, IntervalSeconds = 2.5, MaxAttempts = 10, BackOffRate = 3.0)]
    private static void FullStep() { }

    [Fact]
    public void Defaults_MatchJava()
    {
        var attr = typeof(StepAttributeTests)
            .GetMethod(nameof(DefaultStep), BindingFlags.Static | BindingFlags.NonPublic)!
            .GetCustomAttribute<StepAttribute>()!;

        Assert.Equal("", attr.Name);
        Assert.False(attr.RetriesAllowed);
        Assert.Equal(StepOptions.DefaultIntervalSeconds, attr.IntervalSeconds);
        Assert.Equal(3, attr.MaxAttempts);
        Assert.Equal(StepOptions.DefaultBackOff, attr.BackOffRate);
    }

    [Fact]
    public void Properties_RoundTrip()
    {
        var attr = typeof(StepAttributeTests)
            .GetMethod(nameof(FullStep), BindingFlags.Static | BindingFlags.NonPublic)!
            .GetCustomAttribute<StepAttribute>()!;

        Assert.Equal("s", attr.Name);
        Assert.True(attr.RetriesAllowed);
        Assert.Equal(2.5, attr.IntervalSeconds);
        Assert.Equal(10, attr.MaxAttempts);
        Assert.Equal(3.0, attr.BackOffRate);
    }

    [Fact]
    public void AttributeUsage_TargetsMethodOnly()
    {
        var usage = typeof(StepAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;
        Assert.Equal(AttributeTargets.Method, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
    }
}
