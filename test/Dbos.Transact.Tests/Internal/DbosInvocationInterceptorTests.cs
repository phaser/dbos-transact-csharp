using System.Reflection;
using Dbos.Transact.Internal;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Internal;

// Castle.DynamicProxy generates proxy types into DynamicProxyGenAssembly2 at runtime.
// ITestService is internal so the assembly needs [InternalsVisibleTo("DynamicProxyGenAssembly2")]
// which is declared in the test project's .csproj.
internal interface ITestService
{
    string PassThrough();

    [Workflow]
    Task<string> RunWorkflowAsync(CancellationToken ct = default);

    [Step]
    Task<int> RunStepAsync();

    [Step]
    ValueTask<long> RunValueTaskStepAsync();

    [Step]
    string RunSyncStep();

    [Step]
    Task RunVoidTaskStepAsync();

    [Workflow]
    Task<string> RunWorkflowWithTokenAsync(string msg, CancellationToken ct);
}

internal sealed class TestServiceImpl : ITestService
{
    public string PassThrough() => "direct";

    [Workflow]
    public Task<string> RunWorkflowAsync(CancellationToken ct = default) =>
        Task.FromResult("workflow");

    [Step]
    public Task<int> RunStepAsync() => Task.FromResult(42);

    [Step]
    public ValueTask<long> RunValueTaskStepAsync() => ValueTask.FromResult(100L);

    [Step]
    public string RunSyncStep() => "sync";

    [Step]
    public Task RunVoidTaskStepAsync() => Task.CompletedTask;

    [Workflow]
    public Task<string> RunWorkflowWithTokenAsync(string msg, CancellationToken ct) =>
        Task.FromResult(msg);
}

public class DbosInvocationInterceptorTests
{
    private static (ITestService proxy, List<(MethodInfo method, CancellationToken ct)> calls)
        BuildProxy(Func<object, string?, MethodInfo, object?[], CancellationToken, Task<object?>> handler)
    {
        var calls = new List<(MethodInfo, CancellationToken)>();
        DbosInvocationInterceptor.InvocationHandler wrapping = (target, instanceName, method, args, ct) =>
        {
            calls.Add((method, ct));
            return handler(target, instanceName, method, args, ct);
        };
        var proxy = DbosProxyFactory.CreateProxy<ITestService>(new TestServiceImpl(), null, wrapping);
        return (proxy, calls);
    }

    [Fact]
    public void NonAnnotatedMethod_PassesThrough()
    {
        var (proxy, calls) = BuildProxy((_, _, _, _, _) => Task.FromResult<object?>("intercepted"));
        var result = proxy.PassThrough();
        Assert.Equal("direct", result);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task WorkflowMethod_IsIntercepted()
    {
        var (proxy, calls) = BuildProxy((_, _, _, _, _) => Task.FromResult<object?>("intercepted"));
        var result = await proxy.RunWorkflowAsync();
        Assert.Equal("intercepted", result);
        Assert.Single(calls);
        Assert.Equal(nameof(TestServiceImpl.RunWorkflowAsync), calls[0].method.Name);
    }

    [Fact]
    public async Task StepMethod_IsIntercepted()
    {
        var (proxy, calls) = BuildProxy((_, _, _, _, _) => Task.FromResult<object?>((object)99));
        var result = await proxy.RunStepAsync();
        Assert.Equal(99, result);
        Assert.Single(calls);
    }

    [Fact]
    public async Task ValueTaskStepMethod_ReturnsCorrectValue()
    {
        var (proxy, _) = BuildProxy((_, _, _, _, _) => Task.FromResult<object?>((object)77L));
        var result = await proxy.RunValueTaskStepAsync();
        Assert.Equal(77L, result);
    }

    [Fact]
    public void SyncStepMethod_ReturnsValue()
    {
        var (proxy, _) = BuildProxy((_, _, _, _, _) => Task.FromResult<object?>("sync-result"));
        var result = proxy.RunSyncStep();
        Assert.Equal("sync-result", result);
    }

    [Fact]
    public async Task VoidTaskStepMethod_Completes()
    {
        var (proxy, calls) = BuildProxy((_, _, _, _, _) => Task.FromResult<object?>(null));
        await proxy.RunVoidTaskStepAsync();
        Assert.Single(calls);
    }

    [Fact]
    public async Task CancellationToken_IsExtractedAndPassed()
    {
        using var cts = new CancellationTokenSource();
        var capturedCt = CancellationToken.None;
        var (proxy, _) = BuildProxy((_, _, _, _, ct) =>
        {
            capturedCt = ct;
            return Task.FromResult<object?>("ok");
        });
        await proxy.RunWorkflowWithTokenAsync("hello", cts.Token);
        Assert.Equal(cts.Token, capturedCt);
    }

    [Fact]
    public async Task ExceptionFromHandler_IsReThrownWithoutTargetInvocationExceptionWrapper()
    {
        var (proxy, _) = BuildProxy((_, _, _, _, _) =>
            Task.FromException<object?>(new InvalidOperationException("boom")));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => proxy.RunWorkflowAsync());
        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public void SyncException_IsReThrownWithoutTargetInvocationExceptionWrapper()
    {
        var (proxy, _) = BuildProxy((_, _, _, _, _) =>
            Task.FromException<object?>(new ArgumentException("bad")));

        var ex = Assert.Throws<ArgumentException>(() => proxy.RunSyncStep());
        Assert.Equal("bad", ex.Message);
    }

    [Fact]
    public void NonInterfaceTarget_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            DbosProxyFactory.CreateProxy<TestServiceImpl>(new TestServiceImpl(), null,
                (_, _, _, _, _) => Task.FromResult<object?>(null)));
    }
}
