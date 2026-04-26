using Dbos.Transact.Workflow.Internal;

namespace Dbos.Transact.Tests.Workflow.Internal;

public class WorkflowHandleTcsTests
{
    [Fact]
    public void WorkflowId_ReturnsConstructedValue()
    {
        var tcs = new TaskCompletionSource<int>();
        var handle = new WorkflowHandleTcs<int>("wf-abc", tcs);

        Assert.Equal("wf-abc", handle.WorkflowId);
    }

    [Fact]
    public async Task GetResultAsync_TcsCompleted_ReturnsResult()
    {
        var tcs = new TaskCompletionSource<string>();
        var handle = new WorkflowHandleTcs<string>("wf-1", tcs);

        tcs.SetResult("hello");
        var result = await handle.GetResultAsync();

        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task GetResultAsync_TcsCompletedBeforeAwait_ReturnsImmediately()
    {
        var tcs = new TaskCompletionSource<int>();
        tcs.SetResult(42);
        var handle = new WorkflowHandleTcs<int>("wf-2", tcs);

        var result = await handle.GetResultAsync();

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task GetResultAsync_TcsFaulted_PropagatesException()
    {
        var tcs = new TaskCompletionSource<string>();
        var handle = new WorkflowHandleTcs<string>("wf-3", tcs);

        tcs.SetException(new InvalidOperationException("workflow failed"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => handle.GetResultAsync());
    }

    [Fact]
    public async Task GetResultAsync_CancellationToken_CancelsWait()
    {
        var tcs = new TaskCompletionSource<int>();
        var handle = new WorkflowHandleTcs<int>("wf-4", tcs);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(() => handle.GetResultAsync(cts.Token));
    }

    [Fact]
    public async Task GetStatusAsync_NullDb_ReturnsNull()
    {
        var tcs = new TaskCompletionSource<int>();
        var handle = new WorkflowHandleTcs<int>("wf-5", tcs);

        var status = await handle.GetStatusAsync();

        Assert.Null(status);
    }
}
