using Dbos.Transact.Exceptions;

namespace Dbos.Transact.Tests.Exceptions;

public class DbosMaxRecoveryAttemptsExceededExceptionTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var ex = new DbosMaxRecoveryAttemptsExceededException("wf-1", 5);
        Assert.Equal("wf-1", ex.WorkflowId);
        Assert.Equal(5, ex.MaxRetries);
    }

    [Fact]
    public void Constructor_FormatsMessage()
    {
        var ex = new DbosMaxRecoveryAttemptsExceededException("wf-1", 5);
        Assert.Equal(
            "Workflow wf-1 has been moved to the dead-letter queue after exceeding the maximum of 5 retries",
            ex.Message);
    }

    [Fact]
    public void IsThrowable_AsDbosException()
    {
        var ex = new DbosMaxRecoveryAttemptsExceededException("wf-1", 3);
        Assert.IsAssignableFrom<DbosException>(ex);
    }
}
