using Dbos.Transact.Workflow.Internal;

namespace Dbos.Transact.Tests.Workflow.Internal;

public sealed class StepResultTests
{
    private static StepResult Base() => new("wf-1", 1, "MyFunc");

    [Fact]
    public void Constructor_RequiredFields_Set()
    {
        var r = Base();
        Assert.Equal("wf-1", r.WorkflowId);
        Assert.Equal(1, r.StepId);
        Assert.Equal("MyFunc", r.FunctionName);
        Assert.Null(r.Output);
        Assert.Null(r.Error);
        Assert.Null(r.ChildWorkflowId);
        Assert.Null(r.Serialization);
    }

    [Fact]
    public void WithOutput_ReturnsNewRecordWithOutput()
    {
        var r = Base().WithOutput("result");
        Assert.Equal("result", r.Output);
        Assert.Null(r.Error);
    }

    [Fact]
    public void WithError_ReturnsNewRecordWithError()
    {
        var r = Base().WithError("oops");
        Assert.Equal("oops", r.Error);
        Assert.Null(r.Output);
    }

    [Fact]
    public void WithChildWorkflowId_ReturnsNewRecord()
    {
        var r = Base().WithChildWorkflowId("child-wf");
        Assert.Equal("child-wf", r.ChildWorkflowId);
    }

    [Fact]
    public void WithSerialization_ReturnsNewRecord()
    {
        var r = Base().WithSerialization("csharp_stj");
        Assert.Equal("csharp_stj", r.Serialization);
    }

    [Fact]
    public void With_ImmutableOriginal()
    {
        var original = Base();
        var modified = original.WithOutput("x");

        Assert.Null(original.Output);
        Assert.Equal("x", modified.Output);
    }
}
