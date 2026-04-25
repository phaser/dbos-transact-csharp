using Dbos.Transact.Context;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Context;

public class DbosContextTests
{
    [Fact]
    public void DefaultCtor_NotInWorkflow()
    {
        var ctx = new DbosContext();
        Assert.False(ctx.IsInWorkflow);
        Assert.False(ctx.IsInStep);
        Assert.Null(ctx.WorkflowId);
        Assert.Null(ctx.StepId);
    }

    [Fact]
    public void WorkflowCtor_IsInWorkflow()
    {
        var ctx = new DbosContext("wf-1", null, null, null);
        Assert.True(ctx.IsInWorkflow);
        Assert.Equal("wf-1", ctx.WorkflowId);
        Assert.Equal(0, ctx.CurrentFunctionId);
    }

    [Fact]
    public void GetAndIncrementFunctionId_Increments()
    {
        var ctx = new DbosContext("wf-1", null, null, null);
        Assert.Equal(0, ctx.GetAndIncrementFunctionId());
        Assert.Equal(1, ctx.GetAndIncrementFunctionId());
        Assert.Equal(2, ctx.CurrentFunctionId);
    }

    [Fact]
    public void StepFunctionId_SetAndReset()
    {
        var ctx = new DbosContext("wf-1", null, null, null);
        Assert.False(ctx.IsInStep);
        ctx.SetStepFunctionId(5);
        Assert.True(ctx.IsInStep);
        Assert.Equal(5, ctx.StepId);
        ctx.ResetStepFunctionId();
        Assert.False(ctx.IsInStep);
    }

    [Fact]
    public void CopyCtor_ClonesAllFields()
    {
        var parent = new WorkflowInfo("parent-wf", 2);
        var original = new DbosContext("wf-1", parent, TimeSpan.FromSeconds(30), DateTimeOffset.UtcNow);
        original.NextWorkflowId = "pending-id";
        var copy = new DbosContext(original);

        Assert.Equal(original.WorkflowId, copy.WorkflowId);
        Assert.Equal(original.CurrentFunctionId, copy.CurrentFunctionId);
        Assert.Same(original.Parent, copy.Parent);
        Assert.Equal(original.Timeout, copy.Timeout);
        Assert.Equal(original.Deadline, copy.Deadline);
        Assert.Equal("pending-id", copy.NextWorkflowId);
    }

    [Fact]
    public void CopyCtor_OverridesFunctionId()
    {
        var original = new DbosContext("wf-1", null, null, null);
        var copy = new DbosContext(original, functionId: 7);
        Assert.Equal(7, copy.CurrentFunctionId);
    }

    [Fact]
    public void ConsumeNextWorkflowId_ClearsAfterRead()
    {
        var ctx = new DbosContext("wf-1", null, null, null);
        ctx.NextWorkflowId = "child-id";
        Assert.Equal("child-id", ctx.ConsumeNextWorkflowId());
        Assert.Null(ctx.NextWorkflowId);
        Assert.Null(ctx.ConsumeNextWorkflowId());
    }

    [Fact]
    public void ConsumeNextWorkflowId_UsesFallbackWhenNull()
    {
        var ctx = new DbosContext("wf-1", null, null, null);
        Assert.Equal("fallback", ctx.ConsumeNextWorkflowId("fallback"));
    }

    [Fact]
    public void Serialization_DefaultIsDefault()
    {
        var ctx = new DbosContext("wf-1", null, null, null);
        Assert.Equal(SerializationStrategy.Default, ctx.Serialization);
    }

    [Fact]
    public void SetSerializationStrategy_Updates()
    {
        var ctx = new DbosContext("wf-1", null, null, null);
        ctx.SetSerializationStrategy(SerializationStrategy.Portable);
        Assert.Equal(SerializationStrategy.Portable, ctx.Serialization);
    }
}
