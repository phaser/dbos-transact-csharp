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

    [Fact]
    public void WithName_ReturnsRenamedCopy()
    {
        var q = new Queue("a").WithName("b");
        Assert.Equal("b", q.Name);
    }

    [Fact]
    public void WithConcurrency_SetsAndClears()
    {
        var q = new Queue("q").WithConcurrency(4);
        Assert.Equal(4, q.Concurrency);
        Assert.Null(q.WithConcurrency(null).Concurrency);
    }

    [Fact]
    public void WithWorkerConcurrency_SetsAndClears()
    {
        var q = new Queue("q").WithWorkerConcurrency(2);
        Assert.Equal(2, q.WorkerConcurrency);
        Assert.Null(q.WithWorkerConcurrency(null).WorkerConcurrency);
    }

    [Fact]
    public void WithPriorityEnabled_TogglesFlag()
    {
        var q = new Queue("q").WithPriorityEnabled(true);
        Assert.True(q.PriorityEnabled);
        Assert.False(q.WithPriorityEnabled(false).PriorityEnabled);
    }

    [Fact]
    public void WithPartitioningEnabled_TogglesFlag()
    {
        var q = new Queue("q").WithPartitioningEnabled(true);
        Assert.True(q.PartitioningEnabled);
        Assert.False(q.WithPartitioningEnabled(false).PartitioningEnabled);
    }

    [Fact]
    public void WithRateLimit_Duration_SetsLimiter()
    {
        var q = new Queue("q").WithRateLimit(5, TimeSpan.FromSeconds(10));
        Assert.NotNull(q.RateLimit);
        Assert.Equal(5, q.RateLimit!.Limit);
        Assert.Equal(TimeSpan.FromSeconds(10), q.RateLimit.Period);
        Assert.True(q.HasLimiter);
    }

    [Fact]
    public void WithRateLimit_Seconds_SetsLimiter()
    {
        var q = new Queue("q").WithRateLimit(3, periodSeconds: 1.5);
        Assert.NotNull(q.RateLimit);
        Assert.Equal(3, q.RateLimit!.Limit);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), q.RateLimit.Period);
    }

    [Fact]
    public void WithRateLimit_Null_ClearsLimiter()
    {
        var q = new Queue("q").WithRateLimit(5, TimeSpan.FromSeconds(10)).WithRateLimit(null);
        Assert.Null(q.RateLimit);
        Assert.False(q.HasLimiter);
    }
}
