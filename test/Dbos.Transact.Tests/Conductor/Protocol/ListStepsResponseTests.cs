using System.Globalization;
using System.Text.Json;
using Dbos.Transact.Conductor.Protocol;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Conductor.Protocol;

public class ListStepsResponseTests
{
    [Fact]
    public void StepEntry_DefaultConstructor_ProducesNullFields()
    {
        var step = new ListStepsResponse.StepEntry();
        Assert.Null(step.FunctionName);
        Assert.Null(step.Output);
        Assert.Null(step.Error);
    }

    [Fact]
    public void StepEntry_FromStepInfo_MapsAllFields()
    {
        var info = new StepInfo(
            FunctionId: 3,
            FunctionName: "sendEmail",
            Output: "ok",
            Error: null,
            ChildWorkflowId: "child-wf",
            StartedAt: new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CompletedAt: new DateTimeOffset(2024, 1, 1, 0, 0, 1, TimeSpan.Zero),
            Serialization: null);

        var step = new ListStepsResponse.StepEntry(info);
        Assert.Equal(3, step.FunctionId);
        Assert.Equal("sendEmail", step.FunctionName);
        Assert.Equal("\"ok\"", step.Output);
        Assert.Null(step.Error);
        Assert.Equal("child-wf", step.ChildWorkflowId);
        Assert.Equal(info.StartedAtEpochMs!.Value.ToString(CultureInfo.InvariantCulture), step.StartedAtEpochMs);
        Assert.Equal(info.CompletedAtEpochMs!.Value.ToString(CultureInfo.InvariantCulture), step.CompletedAtEpochMs);
    }

    [Fact]
    public void StepEntry_WithError_FormatsAsClassNameMessage()
    {
        var info = new StepInfo(1, "step", null,
            new ErrorResult("System.TimeoutException", "timed out", null, null, null),
            null, null, null, null);
        var step = new ListStepsResponse.StepEntry(info);
        Assert.Equal("System.TimeoutException: timed out", step.Error);
    }

    [Fact]
    public void ListStepsResponse_Output_Serializes()
    {
        var info = new StepInfo(1, "fn", 42, null, null, null, null, null);
        var req = new ListStepsRequest();
        var response = new ListStepsResponse(req, [new ListStepsResponse.StepEntry(info)]);
        var json = JsonSerializer.Serialize(response);
        Assert.Contains("\"output\"", json);
        Assert.Contains("\"function_name\":\"fn\"", json);
    }

    [Fact]
    public void ListStepsResponse_ExceptionConstructor_SetsError()
    {
        var req = new ListStepsRequest();
        var ex = new InvalidOperationException("bad input");
        var response = new ListStepsResponse(req, ex);
        Assert.Equal("bad input", response.ErrorMessage);
        Assert.Empty(response.Output);
    }
}
