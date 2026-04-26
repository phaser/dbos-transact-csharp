using Dbos.Transact.Json;

namespace Dbos.Transact.Tests.Json;

public sealed class PortableWorkflowExceptionTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var ex = new PortableWorkflowException("DBError", "something failed", code: 42, errorPayload: "detail");

        Assert.Equal("DBError", ex.ErrorName);
        Assert.Equal("something failed", ex.Message);
        Assert.Equal(42, ex.Code);
        Assert.Equal("detail", ex.ErrorPayload);
    }

    [Fact]
    public void Constructor_OptionalFields_AreNull()
    {
        var ex = new PortableWorkflowException("E", "msg");

        Assert.Null(ex.Code);
        Assert.Null(ex.ErrorPayload);
    }

    [Fact]
    public void FromErrorData_RoundTripsViaToErrorData()
    {
        var data = new JsonWorkflowErrorData("TestError", "test message", Code: 7, Data: "payload");

        var ex = PortableWorkflowException.FromErrorData(data);
        var roundTripped = ex.ToErrorData();

        Assert.Equal(data.Name, roundTripped.Name);
        Assert.Equal(data.Message, roundTripped.Message);
        Assert.Equal(data.Code, roundTripped.Code);
        Assert.Equal(data.Data, roundTripped.Data);
    }

    [Fact]
    public void IsException_CanBeCaught()
    {
        static void Throw() => throw new PortableWorkflowException("Err", "boom");

        Assert.Throws<PortableWorkflowException>(Throw);
    }

    [Fact]
    public void ToErrorData_ProducesCorrectRecord()
    {
        var ex = new PortableWorkflowException("MyError", "oops", code: 404);
        var data = ex.ToErrorData();

        Assert.Equal("MyError", data.Name);
        Assert.Equal("oops", data.Message);
        Assert.Equal(404, data.Code);
        Assert.Null(data.Data);
    }
}
