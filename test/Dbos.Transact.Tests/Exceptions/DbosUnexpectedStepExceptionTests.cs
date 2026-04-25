using Dbos.Transact.Exceptions;

namespace Dbos.Transact.Tests.Exceptions;

public class DbosUnexpectedStepExceptionTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var ex = new DbosUnexpectedStepException("wf-1", 3, "StepB", "StepA");
        Assert.Equal("wf-1", ex.WorkflowId);
        Assert.Equal(3, ex.StepId);
        Assert.Equal("StepB", ex.AttemptedName);
        Assert.Equal("StepA", ex.RecordedName);
    }

    [Fact]
    public void Constructor_FormatsMessage()
    {
        var ex = new DbosUnexpectedStepException("wf-1", 3, "StepB", "StepA");
        Assert.Equal(
            "During reexecution of workflow wf-1 step 3, function StepB was attempted but StepA was executed in the original execution. Check that your workflow is deterministic.",
            ex.Message);
    }

    [Fact]
    public void IsThrowable_AsDbosException()
    {
        var ex = new DbosUnexpectedStepException("wf-1", 1, "A", "B");
        Assert.IsAssignableFrom<DbosException>(ex);
    }
}
