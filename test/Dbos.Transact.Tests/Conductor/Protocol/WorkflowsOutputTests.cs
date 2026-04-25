using System.Globalization;
using Dbos.Transact.Conductor.Protocol;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Conductor.Protocol;

public class WorkflowsOutputTests
{
    private static WorkflowStatus MinimalStatus(
        string workflowId = "wf-1",
        WorkflowState state = WorkflowState.Pending,
        ErrorResult? error = null,
        int? priority = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? startedAt = null) =>
        new(workflowId, state, "myWorkflow", "MyClass", "inst",
            null, null, null, null, null, error,
            "exec-1", createdAt, null, "1.0", null, null,
            null, null, null, startedAt, null, priority, null, null, null, null, null, null);

    [Fact]
    public void Constructor_MapsBasicFields()
    {
        var output = new WorkflowsOutput(MinimalStatus("wf-99"));
        Assert.Equal("wf-99", output.WorkflowUuid);
        Assert.Equal("Pending", output.Status);
        Assert.Equal("myWorkflow", output.WorkflowName);
        Assert.Equal("MyClass", output.WorkflowClassName);
        Assert.Equal("exec-1", output.ExecutorId);
        Assert.Equal("1.0", output.ApplicationVersion);
    }

    [Fact]
    public void Constructor_NullError_ErrorIsNull()
    {
        var output = new WorkflowsOutput(MinimalStatus(error: null));
        Assert.Null(output.Error);
    }

    [Fact]
    public void Constructor_NonNullError_FormatsAsClassNameMessage()
    {
        var err = new ErrorResult("System.IO.IOException", "file not found", null, null, null);
        var output = new WorkflowsOutput(MinimalStatus(error: err));
        Assert.Equal("System.IO.IOException: file not found", output.Error);
    }

    [Fact]
    public void Constructor_NullPriority_DefaultsToZeroString()
    {
        var output = new WorkflowsOutput(MinimalStatus(priority: null));
        Assert.Equal("0", output.Priority);
    }

    [Fact]
    public void Constructor_ExplicitPriority_Serialized()
    {
        var output = new WorkflowsOutput(MinimalStatus(priority: 5));
        Assert.Equal("5", output.Priority);
    }

    [Fact]
    public void Constructor_CreatedAt_IsEpochMs()
    {
        var ts = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var output = new WorkflowsOutput(MinimalStatus(createdAt: ts));
        Assert.Equal(ts.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture), output.CreatedAt);
    }

    [Fact]
    public void Constructor_StartedAt_MappedToDequeuedAt()
    {
        var ts = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var output = new WorkflowsOutput(MinimalStatus(startedAt: ts));
        Assert.Equal(ts.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture), output.DequeuedAt);
    }

    [Fact]
    public void DefaultConstructor_ProducesNullFields()
    {
        var output = new WorkflowsOutput();
        Assert.Null(output.WorkflowUuid);
        Assert.Null(output.Status);
    }
}
