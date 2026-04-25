using Dbos.Transact.Exceptions;

namespace Dbos.Transact.Tests.Exceptions;

public class DbosWorkflowExecutionConflictExceptionTests
{
    [Fact]
    public void Constructor_SetsWorkflowId()
    {
        var ex = new DbosWorkflowExecutionConflictException("wf-99");
        Assert.Equal("wf-99", ex.WorkflowId);
    }

    [Fact]
    public void Constructor_FormatsMessage()
    {
        var ex = new DbosWorkflowExecutionConflictException("wf-99");
        Assert.Equal("Conflicting workflow ID wf-99", ex.Message);
    }

    [Fact]
    public void IsThrowable_AsDbosException()
    {
        var ex = new DbosWorkflowExecutionConflictException("wf-1");
        Assert.IsAssignableFrom<DbosException>(ex);
    }
}
