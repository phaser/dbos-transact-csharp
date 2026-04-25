using System.Reflection;
using Dbos.Transact.Internal;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Internal;

public class WorkflowRegistryTests
{
    private static MethodInfo GetMethod(Type type, string name = "Run") =>
        type.GetMethod(name, BindingFlags.Public | BindingFlags.Static)!;

    [Fact]
    public void GetWorkflowClassName_WithoutAttribute_UsesFullTypeName()
    {
        var name = WorkflowRegistry.GetWorkflowClassName(new Plain());
        Assert.Contains("Plain", name);
    }

    [Fact]
    public void GetWorkflowClassName_WithAttribute_UsesAttributeValue()
    {
        var name = WorkflowRegistry.GetWorkflowClassName(new Named());
        Assert.Equal("PortableName", name);
    }

    [Fact]
    public void GetWorkflowName_WithAttributeName_UsesAttributeName()
    {
        var attr = new WorkflowAttribute { Name = "myFlow" };
        Assert.Equal("myFlow", WorkflowRegistry.GetWorkflowName(attr, GetMethod(typeof(Plain))));
    }

    [Fact]
    public void GetWorkflowName_EmptyAttributeName_UsesMethodName()
    {
        var attr = new WorkflowAttribute { Name = "" };
        Assert.Equal("Run", WorkflowRegistry.GetWorkflowName(attr, GetMethod(typeof(Plain))));
    }

    [Fact]
    public void RegisterInstance_Duplicate_ThrowsInvalidOperationException()
    {
        var registry = new WorkflowRegistry();
        registry.RegisterInstance(null, new Plain());
        Assert.Throws<InvalidOperationException>(() => registry.RegisterInstance(null, new Plain()));
    }

    [Fact]
    public void RegisterInstance_DifferentInstanceNames_BothSucceed()
    {
        var registry = new WorkflowRegistry();
        registry.RegisterInstance("a", new Plain());
        registry.RegisterInstance("b", new Plain());
        Assert.Equal(2, registry.GetInstanceSnapshot().Count);
    }

    [Fact]
    public void RegisterWorkflow_Duplicate_ThrowsInvalidOperationException()
    {
        var registry = new WorkflowRegistry();
        var attr = new WorkflowAttribute();
        var method = GetMethod(typeof(Plain));
        registry.RegisterWorkflow(attr, new Plain(), method, null);
        Assert.Throws<InvalidOperationException>(() => registry.RegisterWorkflow(attr, new Plain(), method, null));
    }

    [Fact]
    public void GetWorkflowSnapshot_ReturnsRegisteredEntry()
    {
        var registry = new WorkflowRegistry();
        registry.RegisterWorkflow(new WorkflowAttribute(), new Plain(), GetMethod(typeof(Plain)), null);
        var snap = registry.GetWorkflowSnapshot();
        Assert.Single(snap);
        Assert.Contains("Run", snap.Keys.First());
    }

    [Fact]
    public void GetInstanceSnapshot_ReturnsRegisteredEntry()
    {
        var registry = new WorkflowRegistry();
        registry.RegisterInstance("svc", new Plain());
        var snap = registry.GetInstanceSnapshot();
        Assert.Single(snap);
    }

    [Fact]
    public void GetInstanceSnapshot_IsSnapshot_NotLiveView()
    {
        var registry = new WorkflowRegistry();
        var snap1 = registry.GetInstanceSnapshot();
        registry.RegisterInstance(null, new Plain());
        Assert.Empty(snap1);
    }

    public class Plain
    {
        public static void Run() { }
    }

    [WorkflowClassName("PortableName")]
    public class Named
    {
        public static void Run() { }
    }
}
