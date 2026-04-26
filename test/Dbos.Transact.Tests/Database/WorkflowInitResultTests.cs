using Dbos.Transact.Database;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Database;

public sealed class WorkflowInitResultTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var result = new WorkflowInitResult(
            Status: WorkflowState.Success,
            DeadlineEpochMs: 1_700_000_000_000L,
            ShouldExecuteOnThisExecutor: true,
            Serialization: "csharp_stj");

        Assert.Equal(WorkflowState.Success, result.Status);
        Assert.Equal(1_700_000_000_000L, result.DeadlineEpochMs);
        Assert.True(result.ShouldExecuteOnThisExecutor);
        Assert.Equal("csharp_stj", result.Serialization);
    }

    [Fact]
    public void NullableFields_CanBeNull()
    {
        var result = new WorkflowInitResult(WorkflowState.Pending, null, false, null);

        Assert.Null(result.DeadlineEpochMs);
        Assert.Null(result.Serialization);
    }
}
