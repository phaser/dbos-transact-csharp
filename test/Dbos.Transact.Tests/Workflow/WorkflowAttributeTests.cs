using System.Reflection;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Workflow;

public class WorkflowAttributeTests
{
    [Workflow]
    private static void AnnotatedMethod() { }

    [Workflow(Name = "custom", MaxRecoveryAttempts = 5, SerializationStrategy = SerializationStrategy.Portable)]
    private static void FullyAnnotatedMethod() { }

    [Fact]
    public void Defaults_MatchJava()
    {
        var attr = typeof(WorkflowAttributeTests)
            .GetMethod(nameof(AnnotatedMethod), BindingFlags.Static | BindingFlags.NonPublic)!
            .GetCustomAttribute<WorkflowAttribute>()!;

        Assert.Equal("", attr.Name);
        Assert.Equal(-1, attr.MaxRecoveryAttempts);
        Assert.Equal(SerializationStrategy.Default, attr.SerializationStrategy);
    }

    [Fact]
    public void Properties_RoundTrip()
    {
        var attr = typeof(WorkflowAttributeTests)
            .GetMethod(nameof(FullyAnnotatedMethod), BindingFlags.Static | BindingFlags.NonPublic)!
            .GetCustomAttribute<WorkflowAttribute>()!;

        Assert.Equal("custom", attr.Name);
        Assert.Equal(5, attr.MaxRecoveryAttempts);
        Assert.Equal(SerializationStrategy.Portable, attr.SerializationStrategy);
    }

    [Fact]
    public void AttributeUsage_TargetsMethodOnly()
    {
        var usage = typeof(WorkflowAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;
        Assert.Equal(AttributeTargets.Method, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
    }
}
