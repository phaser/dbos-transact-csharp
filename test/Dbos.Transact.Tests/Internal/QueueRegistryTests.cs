using Dbos.Transact.Internal;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Internal;

public class QueueRegistryTests
{
    [Fact]
    public void Register_ReservedName_ThrowsArgumentException()
    {
        var registry = new QueueRegistry();
        Assert.Throws<ArgumentException>(() =>
            registry.Register(new Queue(Constants.DbosInternalQueue, null, null, false, false, null)));
    }

    [Fact]
    public void Register_WorkerConcurrencyExceedsConcurrency_ThrowsArgumentException()
    {
        var registry = new QueueRegistry();
        var q = new Queue("q", 2, 5, false, false, null);
        Assert.Throws<ArgumentException>(() => registry.Register(q));
    }

    [Fact]
    public void Register_Duplicate_IsNoOp_DoesNotThrow()
    {
        var registry = new QueueRegistry();
        var q = new Queue("my-queue");
        registry.Register(q);
        registry.Register(q);
        Assert.Single(registry.GetSnapshot(), x => x.Name == "my-queue");
    }

    [Fact]
    public void Get_RegisteredQueue_ReturnsIt()
    {
        var registry = new QueueRegistry();
        registry.Register(new Queue("wq"));
        var q = registry.Get("wq");
        Assert.NotNull(q);
        Assert.Equal("wq", q.Name);
    }

    [Fact]
    public void Get_InternalQueue_ReturnsInternalQueue()
    {
        var registry = new QueueRegistry();
        var q = registry.Get(Constants.DbosInternalQueue);
        Assert.NotNull(q);
        Assert.Equal(Constants.DbosInternalQueue, q.Name);
    }

    [Fact]
    public void Get_UnknownQueue_ReturnsNull()
    {
        var registry = new QueueRegistry();
        Assert.Null(registry.Get("nonexistent"));
    }

    [Fact]
    public void Clear_RemovesRegisteredQueues()
    {
        var registry = new QueueRegistry();
        registry.Register(new Queue("q1"));
        registry.Clear();
        Assert.Null(registry.Get("q1"));
    }

    [Fact]
    public void Clear_DoesNotRemoveInternalQueue()
    {
        var registry = new QueueRegistry();
        registry.Clear();
        Assert.NotNull(registry.Get(Constants.DbosInternalQueue));
    }

    [Fact]
    public void GetSnapshot_IncludesInternalQueue()
    {
        var registry = new QueueRegistry();
        registry.Register(new Queue("user-queue"));
        var snapshot = registry.GetSnapshot();
        Assert.Contains(snapshot, q => q.Name == Constants.DbosInternalQueue);
        Assert.Contains(snapshot, q => q.Name == "user-queue");
    }

    [Fact]
    public void GetSnapshot_AfterClear_OnlyContainsInternalQueue()
    {
        var registry = new QueueRegistry();
        registry.Register(new Queue("q"));
        registry.Clear();
        var snapshot = registry.GetSnapshot();
        Assert.Single(snapshot);
        Assert.Equal(Constants.DbosInternalQueue, snapshot[0].Name);
    }
}
