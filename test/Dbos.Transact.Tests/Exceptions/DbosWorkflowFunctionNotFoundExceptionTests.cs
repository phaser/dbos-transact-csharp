using Dbos.Transact.Exceptions;

namespace Dbos.Transact.Tests.Exceptions;

public class DbosWorkflowFunctionNotFoundExceptionTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var ex = new DbosWorkflowFunctionNotFoundException("wf-5", "MyWorkflow");
        Assert.Equal("wf-5", ex.WorkflowId);
        Assert.Equal("MyWorkflow", ex.WorkflowName);
    }

    [Fact]
    public void Constructor_FormatsMessage()
    {
        var ex = new DbosWorkflowFunctionNotFoundException("wf-5", "MyWorkflow");
        Assert.Equal("Workflow function MyWorkflow does not exist for workflow id wf-5.", ex.Message);
    }

    [Fact]
    public void IsThrowable_AsDbosException()
    {
        var ex = new DbosWorkflowFunctionNotFoundException("wf-1", "Fn");
        Assert.IsAssignableFrom<DbosException>(ex);
    }
}
