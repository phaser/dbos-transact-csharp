using System.Reflection;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Workflow;

public class WorkflowClassNameAttributeTests
{
    [Fact]
    public void Value_RoundTrip()
    {
        var attr = typeof(ServiceStub).GetCustomAttribute<WorkflowClassNameAttribute>()!;
        Assert.Equal("MyPortableService", attr.Value);
    }

    [Fact]
    public void AttributeUsage_TargetsClassAndInterface()
    {
        var usage = typeof(WorkflowClassNameAttribute).GetCustomAttribute<AttributeUsageAttribute>()!;
        Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Class));
        Assert.True(usage.ValidOn.HasFlag(AttributeTargets.Interface));
        Assert.False(usage.AllowMultiple);
    }

    [WorkflowClassName("MyPortableService")]
    private sealed class ServiceStub { }
}
