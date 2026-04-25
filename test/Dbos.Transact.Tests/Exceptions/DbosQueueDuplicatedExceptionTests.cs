using Dbos.Transact.Exceptions;

namespace Dbos.Transact.Tests.Exceptions;

public class DbosQueueDuplicatedExceptionTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var ex = new DbosQueueDuplicatedException("wf-1", "my-queue", "dedup-id");
        Assert.Equal("wf-1", ex.WorkflowId);
        Assert.Equal("my-queue", ex.QueueName);
        Assert.Equal("dedup-id", ex.DeduplicationId);
    }

    [Fact]
    public void Constructor_FormatsMessage()
    {
        var ex = new DbosQueueDuplicatedException("wf-1", "my-queue", "dedup-id");
        Assert.Equal(
            "Workflow wf-1 (Queue: my-queue, Deduplication ID: dedup-id) is already enqueued.",
            ex.Message);
    }

    [Fact]
    public void IsThrowable_AsDbosException()
    {
        var ex = new DbosQueueDuplicatedException("wf-1", "q", "d");
        Assert.IsAssignableFrom<DbosException>(ex);
    }
}
