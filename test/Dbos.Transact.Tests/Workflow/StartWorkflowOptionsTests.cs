using Dbos.Transact;
using Dbos.Transact.Workflow;
using Timeout = Dbos.Transact.Workflow.Timeout;

namespace Dbos.Transact.Tests.Workflow;

public class StartWorkflowOptionsTests
{
    [Fact]
    public void DefaultCtor_AllNulls()
    {
        var opts = new StartWorkflowOptions();
        Assert.Null(opts.WorkflowId);
        Assert.Null(opts.Timeout);
        Assert.Null(opts.Deadline);
        Assert.Null(opts.QueueName);
        Assert.Null(opts.DeduplicationId);
        Assert.Null(opts.Priority);
        Assert.Null(opts.QueuePartitionKey);
        Assert.Null(opts.Delay);
        Assert.Null(opts.AppVersion);
    }

    [Fact]
    public void IdCtor_SetsId()
    {
        var opts = new StartWorkflowOptions("wf-123");
        Assert.Equal("wf-123", opts.WorkflowId);
    }

    [Fact]
    public void QueueCtor_SetsQueueName()
    {
        var q = new Queue("my-queue");
        var opts = new StartWorkflowOptions(q);
        Assert.Equal("my-queue", opts.QueueName);
    }

    [Theory]
    [InlineData("WorkflowId", "")]
    [InlineData("QueueName", "")]
    [InlineData("DeduplicationId", "")]
    [InlineData("QueuePartitionKey", "")]
    [InlineData("AppVersion", "")]
    public void EmptyStringFields_ThrowArgumentException(string paramName, string emptyValue)
    {
        Assert.Throws<ArgumentException>(() => paramName switch
        {
            "WorkflowId" => new StartWorkflowOptions(emptyValue, null, null, null, null, null, null, null, null),
            "QueueName" => new StartWorkflowOptions(null, null, null, emptyValue, null, null, null, null, null),
            "DeduplicationId" => new StartWorkflowOptions(null, null, null, null, emptyValue, null, null, null, null),
            "QueuePartitionKey" => new StartWorkflowOptions(null, null, null, null, null, null, emptyValue, null, null),
            "AppVersion" => new StartWorkflowOptions(null, null, null, null, null, null, null, null, emptyValue),
            _ => throw new InvalidOperationException()
        });
    }

    [Fact]
    public void ZeroDelay_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StartWorkflowOptions(null, null, null, null, null, null, null, TimeSpan.Zero, null));
    }

    [Fact]
    public void NegativeDelay_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StartWorkflowOptions(null, null, null, null, null, null, null, TimeSpan.FromSeconds(-1), null));
    }

    [Fact]
    public void WithTimeout_SetsExplicitTimeout()
    {
        var opts = new StartWorkflowOptions().WithTimeout(TimeSpan.FromSeconds(60));
        var explicit_ = Assert.IsType<Timeout.Explicit>(opts.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(60), explicit_.Value);
    }

    [Fact]
    public void WithNoTimeout_SetsNoneTimeout()
    {
        var opts = new StartWorkflowOptions().WithNoTimeout();
        Assert.IsType<Timeout.None>(opts.Timeout);
    }

    [Fact]
    public void WithQueue_SetsQueueName()
    {
        var q = new Queue("q1");
        var opts = new StartWorkflowOptions().WithQueue(q);
        Assert.Equal("q1", opts.QueueName);
    }

    [Fact]
    public void RecordEquality_SameValues_Equal()
    {
        var a = new StartWorkflowOptions("wf-1");
        var b = new StartWorkflowOptions("wf-1");
        Assert.Equal(a, b);
    }
}
