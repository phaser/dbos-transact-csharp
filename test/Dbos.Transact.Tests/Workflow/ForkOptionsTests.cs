using Dbos.Transact.Workflow;
using Timeout = Dbos.Transact.Workflow.Timeout;

namespace Dbos.Transact.Tests.Workflow;

public class ForkOptionsTests
{
    [Fact]
    public void DefaultCtor_AllNulls()
    {
        var opts = new ForkOptions();
        Assert.Null(opts.ForkedWorkflowId);
        Assert.Null(opts.ApplicationVersion);
        Assert.Null(opts.Timeout);
        Assert.Null(opts.QueueName);
        Assert.Null(opts.QueuePartitionKey);
    }

    [Fact]
    public void IdCtor_SetsId()
    {
        var opts = new ForkOptions("fork-1");
        Assert.Equal("fork-1", opts.ForkedWorkflowId);
    }

    [Fact]
    public void WithTimeout_SetsExplicitTimeout()
    {
        var opts = new ForkOptions().WithTimeout(TimeSpan.FromMinutes(5));
        var explicit_ = Assert.IsType<Timeout.Explicit>(opts.Timeout);
        Assert.Equal(TimeSpan.FromMinutes(5), explicit_.Value);
    }

    [Fact]
    public void WithNoTimeout_SetsNoneTimeout()
    {
        var opts = new ForkOptions().WithNoTimeout();
        Assert.IsType<Timeout.None>(opts.Timeout);
    }

    [Fact]
    public void ExplicitTimeout_ZeroDuration_ThrowsViaTimeoutExplicit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForkOptions(null, null, new Timeout.Explicit(TimeSpan.Zero), null, null));
    }

    [Fact]
    public void RecordEquality_SameValues_Equal()
    {
        var a = new ForkOptions("wf", "v1", null, null, null);
        var b = new ForkOptions("wf", "v1", null, null, null);
        Assert.Equal(a, b);
    }
}
