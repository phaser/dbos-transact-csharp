using Dbos.Transact.Workflow.Internal;

namespace Dbos.Transact.Tests.Workflow.Internal;

public sealed class WorkflowStatusInternalTests
{
    private static WorkflowStatusInternal Build(
        TimeSpan? timeout = null,
        TimeSpan? delay = null,
        DateTimeOffset? deadline = null) =>
        new(
            WorkflowId: "wf-1",
            WorkflowName: "MyWorkflow",
            ClassName: "MyClass",
            InstanceName: null,
            QueueName: null,
            DeduplicationId: null,
            Priority: null,
            QueuePartitionKey: null,
            Delay: delay,
            AuthenticatedUser: null,
            AssumedRole: null,
            AuthenticatedRoles: null,
            Inputs: null,
            ExecutorId: null,
            AppVersion: null,
            AppId: null,
            Timeout: timeout,
            Deadline: deadline,
            ParentWorkflowId: null,
            Serialization: null);

    [Fact]
    public void TimeoutMs_Null_ReturnsNull()
    {
        var s = Build();
        Assert.Null(s.TimeoutMs);
    }

    [Fact]
    public void TimeoutMs_Set_ReturnsMilliseconds()
    {
        var s = Build(timeout: TimeSpan.FromSeconds(5));
        Assert.Equal(5000L, s.TimeoutMs);
    }

    [Fact]
    public void DelayMs_Set_ReturnsMilliseconds()
    {
        var s = Build(delay: TimeSpan.FromMilliseconds(250));
        Assert.Equal(250L, s.DelayMs);
    }

    [Fact]
    public void DeadlineEpochMs_Set_ReturnsEpochMs()
    {
        var deadline = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000L);
        var s = Build(deadline: deadline);
        Assert.Equal(1_700_000_000_000L, s.DeadlineEpochMs);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var a = Build();
        var b = Build();
        Assert.Equal(a, b);
    }
}
