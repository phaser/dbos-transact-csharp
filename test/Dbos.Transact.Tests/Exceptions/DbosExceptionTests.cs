using Dbos.Transact.Exceptions;

namespace Dbos.Transact.Tests.Exceptions;

public class DbosExceptionTests
{
    [Fact]
    public void AllExceptions_AreDbosException()
    {
        Assert.True(typeof(DbosException).IsAssignableFrom(typeof(DbosAwaitedWorkflowCancelledException)));
        Assert.True(typeof(DbosException).IsAssignableFrom(typeof(DbosConflictingWorkflowException)));
        Assert.True(typeof(DbosException).IsAssignableFrom(typeof(DbosMaxRecoveryAttemptsExceededException)));
        Assert.True(typeof(DbosException).IsAssignableFrom(typeof(DbosNonExistentWorkflowException)));
        Assert.True(typeof(DbosException).IsAssignableFrom(typeof(DbosQueueDuplicatedException)));
        Assert.True(typeof(DbosException).IsAssignableFrom(typeof(DbosSystemDatabaseException)));
        Assert.True(typeof(DbosException).IsAssignableFrom(typeof(DbosUnexpectedStepException)));
        Assert.True(typeof(DbosException).IsAssignableFrom(typeof(DbosWorkflowCancelledException)));
        Assert.True(typeof(DbosException).IsAssignableFrom(typeof(DbosWorkflowExecutionConflictException)));
        Assert.True(typeof(DbosException).IsAssignableFrom(typeof(DbosWorkflowFunctionNotFoundException)));
    }

    [Fact]
    public void DbosException_IsAbstract()
    {
        Assert.True(typeof(DbosException).IsAbstract);
    }

    [Fact]
    public void DbosException_InheritsFromException()
    {
        Assert.True(typeof(Exception).IsAssignableFrom(typeof(DbosException)));
    }
}
