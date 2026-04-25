using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Workflow;

public class WorkflowStreamTests
{
    [Fact]
    public void RecordEquality_SameValues_Equal()
    {
        var a = new WorkflowStream("k", "v", 0, 1, "json");
        var b = new WorkflowStream("k", "v", 0, 1, "json");
        Assert.Equal(a, b);
    }

    [Fact]
    public void RecordEquality_DifferentOffset_NotEqual()
    {
        var a = new WorkflowStream("k", "v", 0, 1, null);
        var b = new WorkflowStream("k", "v", 1, 1, null);
        Assert.NotEqual(a, b);
    }
}
