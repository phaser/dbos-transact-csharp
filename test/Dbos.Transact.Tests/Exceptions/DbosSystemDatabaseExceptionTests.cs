using Dbos.Transact.Exceptions;

namespace Dbos.Transact.Tests.Exceptions;

public class DbosSystemDatabaseExceptionTests
{
    [Fact]
    public void Constructor_SetsDatabaseException()
    {
        var inner = new InvalidOperationException("connection refused");
        var ex = new DbosSystemDatabaseException(inner);
        Assert.Same(inner, ex.DatabaseException);
    }

    [Fact]
    public void Constructor_SetsInnerException()
    {
        var inner = new InvalidOperationException("connection refused");
        var ex = new DbosSystemDatabaseException(inner);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void Constructor_FormatsMessage()
    {
        var inner = new InvalidOperationException("connection refused");
        var ex = new DbosSystemDatabaseException(inner);
        Assert.Equal("System database access error: connection refused", ex.Message);
    }

    [Fact]
    public void IsThrowable_AsDbosException()
    {
        var ex = new DbosSystemDatabaseException(new InvalidOperationException("db down"));
        Assert.IsAssignableFrom<DbosException>(ex);
    }
}
