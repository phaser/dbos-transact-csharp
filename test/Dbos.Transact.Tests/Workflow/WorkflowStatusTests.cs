using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Workflow;

public class WorkflowStatusTests
{
    private static WorkflowStatus MakeStatus(string id = "wf-1") =>
        new(id, WorkflowState.Pending, "TestWorkflow", "TestClass", null,
            null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null);

    [Fact]
    public void Equality_SameValues_Equal()
    {
        var a = MakeStatus();
        var b = MakeStatus();
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentId_NotEqual()
    {
        var a = MakeStatus("wf-1");
        var b = MakeStatus("wf-2");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equality_WithArrayFields_StructurallyEqual()
    {
        var a = new WorkflowStatus("wf-1", WorkflowState.Success, null, null, null,
            null, null, ["admin", "user"], ["arg1"], "output",
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
        var b = new WorkflowStatus("wf-1", WorkflowState.Success, null, null, null,
            null, null, ["admin", "user"], ["arg1"], "output",
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentArrays_NotEqual()
    {
        var a = new WorkflowStatus("wf-1", null, null, null, null,
            null, null, ["admin"], null, null,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
        var b = new WorkflowStatus("wf-1", null, null, null, null,
            null, null, ["user"], null, null,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void EpochMs_ComputedFromInstant()
    {
        var now = DateTimeOffset.UtcNow;
        var status = MakeStatus() with { CreatedAt = now };
        Assert.Equal(now.ToUnixTimeMilliseconds(), status.CreatedAtEpochMs);
    }

    [Fact]
    public void TimeoutMs_ComputedFromTimeSpan()
    {
        var status = MakeStatus() with { Timeout = TimeSpan.FromSeconds(30) };
        Assert.Equal(30_000L, status.TimeoutMs);
    }

    [Fact]
    public void GetHashCode_ConsistentWithEquals()
    {
        var a = MakeStatus();
        var b = MakeStatus();
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
