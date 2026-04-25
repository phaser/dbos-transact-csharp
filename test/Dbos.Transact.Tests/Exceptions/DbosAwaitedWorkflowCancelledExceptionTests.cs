using Dbos.Transact.Exceptions;

namespace Dbos.Transact.Tests.Exceptions;

public class DbosAwaitedWorkflowCancelledExceptionTests
{
    [Fact]
    public void Constructor_SetsWorkflowId()
    {
        var ex = new DbosAwaitedWorkflowCancelledException("wf-123");
        Assert.Equal("wf-123", ex.WorkflowId);
    }

    [Fact]
    public void Constructor_FormatsMessage()
    {
        var ex = new DbosAwaitedWorkflowCancelledException("wf-123");
        Assert.Equal("Awaited workflow wf-123 was cancelled.", ex.Message);
    }

    [Fact]
    public void IsThrowable_AsDbosException()
    {
        var ex = new DbosAwaitedWorkflowCancelledException("wf-1");
        Assert.IsAssignableFrom<DbosException>(ex);
    }
}
