using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Workflow;

public class QueueTests
{
    [Fact]
    public void NameCtor_SetsDefaults()
    {
        var q = new Queue("my-queue");
        Assert.Equal("my-queue", q.Name);
        Assert.Null(q.Concurrency);
        Assert.Null(q.WorkerConcurrency);
        Assert.False(q.PriorityEnabled);
        Assert.False(q.PartitioningEnabled);
        Assert.Null(q.RateLimit);
        Assert.False(q.HasLimiter);
    }

    [Fact]
    public void NullOrEmptyName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Queue(""));
        Assert.Throws<ArgumentException>(() => new Queue(string.Empty));
    }

    [Fact]
    public void ZeroConcurrency_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Queue("q", 0, null, false, false, null));
    }

    [Fact]
    public void NegativeConcurrency_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Queue("q", -1, null, false, false, null));
    }

    [Fact]
    public void ZeroWorkerConcurrency_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Queue("q", null, 0, false, false, null));
    }

    [Fact]
    public void RateLimit_HasLimiter_True()
    {
        var q = new Queue("q", null, null, false, false, new RateLimit(10, TimeSpan.FromMinutes(1)));
        Assert.True(q.HasLimiter);
    }

    [Fact]
    public void RecordEquality_SameValues_Equal()
    {
        var a = new Queue("q");
        var b = new Queue("q");
        Assert.Equal(a, b);
    }
}
