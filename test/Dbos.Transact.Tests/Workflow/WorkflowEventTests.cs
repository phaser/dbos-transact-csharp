using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Workflow;

public class WorkflowEventTests
{
    [Fact]
    public void RecordEquality_SameValues_Equal()
    {
        var a = new WorkflowEvent("k", "v", "json");
        var b = new WorkflowEvent("k", "v", "json");
        Assert.Equal(a, b);
    }

    [Fact]
    public void RecordEquality_DifferentKey_NotEqual()
    {
        var a = new WorkflowEvent("k1", "v", null);
        var b = new WorkflowEvent("k2", "v", null);
        Assert.NotEqual(a, b);
    }
}
