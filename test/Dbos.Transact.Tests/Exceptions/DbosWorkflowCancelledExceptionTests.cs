using Dbos.Transact.Exceptions;

namespace Dbos.Transact.Tests.Exceptions;

public class DbosWorkflowCancelledExceptionTests
{
    [Fact]
    public void Constructor_SetsWorkflowId()
    {
        var ex = new DbosWorkflowCancelledException("wf-7");
        Assert.Equal("wf-7", ex.WorkflowId);
    }

    [Fact]
    public void Constructor_FormatsMessage()
    {
        var ex = new DbosWorkflowCancelledException("wf-7");
        Assert.Equal("Workflow wf-7 has been cancelled", ex.Message);
    }

    [Fact]
    public void IsThrowable_AsDbosException()
    {
        var ex = new DbosWorkflowCancelledException("wf-1");
        Assert.IsAssignableFrom<DbosException>(ex);
    }
}
