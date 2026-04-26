using System.Reflection;
using Dbos.Transact.Execution;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Execution;

public class RegisteredWorkflowInvokeTests
{
    private static MethodInfo GetMethod(string name) =>
        typeof(SampleWorkflows).GetMethod(name, BindingFlags.Public | BindingFlags.Static)!;

    [Fact]
    public void Invoke_ReturnsMethodResult()
    {
        var target = new SampleWorkflows();
        var method = GetMethod(nameof(SampleWorkflows.Add));
        var rw = MakeRegistered(target, method);

        var result = rw.Invoke<int>([3, 4]);

        Assert.Equal(7, result);
    }

    [Fact]
    public void Invoke_WorkflowMethodThrows_PropagatesOriginalException()
    {
        var target = new SampleWorkflows();
        var method = GetMethod(nameof(SampleWorkflows.Throw));
        var rw = MakeRegistered(target, method);

        var ex = Assert.Throws<InvalidOperationException>(() => rw.Invoke<object>(null));
        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public void Invoke_DoesNotWrapInTargetInvocationException()
    {
        var target = new SampleWorkflows();
        var method = GetMethod(nameof(SampleWorkflows.Throw));
        var rw = MakeRegistered(target, method);

        try
        {
            rw.Invoke<object>(null);
            Assert.Fail("Expected exception");
        }
        catch (Exception ex)
        {
            Assert.IsNotType<TargetInvocationException>(ex);
        }
    }

    private static RegisteredWorkflow MakeRegistered(object target, MethodInfo method) =>
        new(WorkflowName: method.Name, ClassName: target.GetType().Name, InstanceName: null,
            Target: target, WorkflowMethod: method, MaxRecoveryAttempts: 3,
            SerializationStrategy: SerializationStrategy.Default);

    private sealed class SampleWorkflows
    {
        public static int Add(int a, int b) => a + b;
        public static object Throw() => throw new InvalidOperationException("boom");
    }
}
