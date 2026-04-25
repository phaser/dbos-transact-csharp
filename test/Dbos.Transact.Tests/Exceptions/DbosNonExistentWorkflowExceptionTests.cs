using Dbos.Transact.Exceptions;

namespace Dbos.Transact.Tests.Exceptions;

public class DbosNonExistentWorkflowExceptionTests
{
    [Fact]
    public void Constructor_SetsWorkflowId()
    {
        var ex = new DbosNonExistentWorkflowException("wf-42");
        Assert.Equal("wf-42", ex.WorkflowId);
    }

    [Fact]
    public void Constructor_FormatsMessage()
    {
        var ex = new DbosNonExistentWorkflowException("wf-42");
        Assert.Equal("Workflow does not exist wf-42", ex.Message);
    }

    [Fact]
    public void IsThrowable_AsDbosException()
    {
        var ex = new DbosNonExistentWorkflowException("wf-1");
        Assert.IsAssignableFrom<DbosException>(ex);
    }
}
