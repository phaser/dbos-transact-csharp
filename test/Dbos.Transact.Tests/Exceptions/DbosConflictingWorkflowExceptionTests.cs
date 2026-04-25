using Dbos.Transact.Exceptions;

namespace Dbos.Transact.Tests.Exceptions;

public class DbosConflictingWorkflowExceptionTests
{
    [Fact]
    public void Constructor_SetsWorkflowId()
    {
        var ex = new DbosConflictingWorkflowException("wf-1", "wrong function");
        Assert.Equal("wf-1", ex.WorkflowId);
    }

    [Fact]
    public void Constructor_FormatsMessage()
    {
        var ex = new DbosConflictingWorkflowException("wf-1", "wrong function");
        Assert.Equal("Conflicting workflow invocation with same ID wf-1 : wrong function", ex.Message);
    }

    [Fact]
    public void IsThrowable_AsDbosException()
    {
        var ex = new DbosConflictingWorkflowException("wf-1", "msg");
        Assert.IsAssignableFrom<DbosException>(ex);
    }
}
