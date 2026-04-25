using System.Reflection;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Workflow;

public class ScheduledAttributeTests
{
    [Scheduled("0 * * * *")]
    private static void DefaultScheduled() { }

    [Scheduled("0 0 * * *", Queue = "my-queue", AutomaticBackfill = true)]
    private static void FullScheduled() { }

    [Fact]
    public void Defaults_MatchJava()
    {
        var attr = typeof(ScheduledAttributeTests)
            .GetMethod(nameof(DefaultScheduled), BindingFlags.Static | BindingFlags.NonPublic)!
            .GetCustomAttribute<ScheduledAttribute>()!;

        Assert.Equal("0 * * * *", attr.Cron);
        Assert.Equal("", attr.Queue);
        Assert.False(attr.AutomaticBackfill);
    }

    [Fact]
    public void Properties_RoundTrip()
    {
        var attr = typeof(ScheduledAttributeTests)
            .GetMethod(nameof(FullScheduled), BindingFlags.Static | BindingFlags.NonPublic)!
            .GetCustomAttribute<ScheduledAttribute>()!;

        Assert.Equal("0 0 * * *", attr.Cron);
        Assert.Equal("my-queue", attr.Queue);
        Assert.True(attr.AutomaticBackfill);
    }

    [Fact]
    public void AttributeUsage_TargetsMethodOnly()
    {
        var usage = typeof(ScheduledAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;
        Assert.Equal(AttributeTargets.Method, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
    }
}
