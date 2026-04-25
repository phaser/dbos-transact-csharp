using Dbos.Transact.Admin;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Admin;

public class ListWorkflowsRequestTests
{
    [Fact]
    public void AsInput_AllNullFields_ProducesNullInput()
    {
        var req = new ListWorkflowsRequest(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
        var input = req.AsInput();
        Assert.Null(input.WorkflowIds);
        Assert.Null(input.Status);
        Assert.Null(input.StartTime);
        Assert.Null(input.EndTime);
    }

    [Fact]
    public void AsInput_WorkflowName_WrapsInList()
    {
        var req = new ListWorkflowsRequest(null, "myWorkflow", null, null, null, null, null, null, null, null, null, null, null, null, null);
        var input = req.AsInput();
        Assert.Equal(["myWorkflow"], input.WorkflowName);
    }

    [Fact]
    public void AsInput_Status_ParsesEnum()
    {
        var req = new ListWorkflowsRequest(null, null, null, null, null, "Success", null, null, null, null, null, null, null, null, null);
        var input = req.AsInput();
        Assert.Equal([WorkflowState.Success], input.Status);
    }

    [Fact]
    public void AsInput_InvalidStatus_ThrowsArgumentException()
    {
        var req = new ListWorkflowsRequest(null, null, null, null, null, "NotAState", null, null, null, null, null, null, null, null, null);
        Assert.Throws<ArgumentException>(() => req.AsInput());
    }

    [Fact]
    public void AsInput_StartTime_ParsesDateTimeOffset()
    {
        var req = new ListWorkflowsRequest(null, null, null, "2024-01-15T00:00:00+00:00", null, null, null, null, null, null, null, null, null, null, null);
        var input = req.AsInput();
        Assert.Equal(new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero), input.StartTime);
    }

    [Fact]
    public void AsInput_LimitOffset_PassedThrough()
    {
        var req = new ListWorkflowsRequest(null, null, null, null, null, null, null, null, null, 50, 10, null, null, null, null);
        var input = req.AsInput();
        Assert.Equal(50, input.Limit);
        Assert.Equal(10, input.Offset);
    }

    [Fact]
    public void AsInput_SortDesc_PassedThrough()
    {
        var req = new ListWorkflowsRequest(null, null, null, null, null, null, null, null, null, null, null, true, null, null, null);
        var input = req.AsInput();
        Assert.True(input.SortDesc);
    }
}
