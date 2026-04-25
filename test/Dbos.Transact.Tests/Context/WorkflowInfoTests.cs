using Dbos.Transact.Context;

namespace Dbos.Transact.Tests.Context;

public class WorkflowInfoTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var info = new WorkflowInfo("wf-1", 3);
        Assert.Equal("wf-1", info.WorkflowId);
        Assert.Equal(3, info.FunctionId);
    }

    [Fact]
    public void RecordEquality_SameValues_Equal()
    {
        var a = new WorkflowInfo("wf-1", 3);
        var b = new WorkflowInfo("wf-1", 3);
        Assert.Equal(a, b);
    }

    [Fact]
    public void RecordEquality_DifferentFunctionId_NotEqual()
    {
        var a = new WorkflowInfo("wf-1", 1);
        var b = new WorkflowInfo("wf-1", 2);
        Assert.NotEqual(a, b);
    }
}
