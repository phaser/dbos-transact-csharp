using Dbos.Transact.Workflow;
using Timeout = Dbos.Transact.Workflow.Timeout;

namespace Dbos.Transact.Tests.Workflow;

public class WorkflowOptionsTests
{
    [Fact]
    public void DefaultCtor_AllNulls()
    {
        var opts = new WorkflowOptions();
        Assert.Null(opts.WorkflowId);
        Assert.Null(opts.Timeout);
        Assert.Null(opts.Deadline);
    }

    [Fact]
    public void SingleIdCtor_SetsId()
    {
        var opts = new WorkflowOptions("wf-1");
        Assert.Equal("wf-1", opts.WorkflowId);
        Assert.Null(opts.Timeout);
    }

    [Fact]
    public void EmptyWorkflowId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new WorkflowOptions(""));
    }

    [Fact]
    public void NullWorkflowId_Allowed()
    {
        var opts = new WorkflowOptions(null, null, null);
        Assert.Null(opts.WorkflowId);
    }

    [Fact]
    public void WithTimeout_SetsExplicitTimeout()
    {
        var opts = new WorkflowOptions().WithTimeout(TimeSpan.FromSeconds(10));
        var explicit_ = Assert.IsType<Timeout.Explicit>(opts.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(10), explicit_.Value);
    }

    [Fact]
    public void WithNoTimeout_SetsNoneTimeout()
    {
        var opts = new WorkflowOptions().WithNoTimeout();
        Assert.IsType<Timeout.None>(opts.Timeout);
    }

    [Fact]
    public void RecordEquality_SameValues_Equal()
    {
        var a = new WorkflowOptions("wf-1", null, null);
        var b = new WorkflowOptions("wf-1", null, null);
        Assert.Equal(a, b);
    }
}
