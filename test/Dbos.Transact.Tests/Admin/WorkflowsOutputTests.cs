using System.Globalization;
using System.Text.Json;
using Dbos.Transact.Admin;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Tests.Admin;

public class WorkflowsOutputTests
{
    private static WorkflowStatus MinimalStatus(
        string workflowId = "wf-1",
        WorkflowState state = WorkflowState.Pending,
        string? queueName = null,
        ErrorResult? error = null,
        string[]? roles = null,
        object?[]? input = null,
        object? output = null) =>
        new(workflowId, state, "myWorkflow", "MyClass", "inst",
            null, null, roles, input, output, error,
            "exec-1", null, null, "1.0", null, null,
            queueName, null, null, null, null, null, null, null, null, null, null, null);

    [Fact]
    public void Of_MapsBasicFields()
    {
        var status = MinimalStatus("wf-42");
        var output = WorkflowsOutput.Of(status);
        Assert.Equal("wf-42", output.WorkflowUuid);
        Assert.Equal("Pending", output.Status);
        Assert.Equal("myWorkflow", output.WorkflowName);
        Assert.Equal("MyClass", output.WorkflowClassName);
        Assert.Equal("inst", output.WorkflowConfigName);
        Assert.Equal("exec-1", output.ExecutorId);
        Assert.Equal("1.0", output.ApplicationVersion);
    }

    [Fact]
    public void Of_NullRoles_SerializesAsEmptyJsonArray()
    {
        var output = WorkflowsOutput.Of(MinimalStatus(roles: null));
        Assert.Equal("[]", output.AuthenticatedRoles);
    }

    [Fact]
    public void Of_NonNullRoles_SerializesAsJsonArray()
    {
        var output = WorkflowsOutput.Of(MinimalStatus(roles: ["admin", "user"]));
        var roles = JsonSerializer.Deserialize<string[]>(output.AuthenticatedRoles!);
        Assert.NotNull(roles);
        Assert.Equal(["admin", "user"], roles);
    }

    [Fact]
    public void Of_NullInput_SerializesAsEmptyJsonArray()
    {
        var output = WorkflowsOutput.Of(MinimalStatus(input: null));
        Assert.Equal("[]", output.Input);
    }

    [Fact]
    public void Of_NonNullInput_SerializesAsJsonArray()
    {
        var output = WorkflowsOutput.Of(MinimalStatus(input: [42, "hello"]));
        Assert.NotNull(output.Input);
        Assert.Contains("42", output.Input);
        Assert.Contains("hello", output.Input);
    }

    [Fact]
    public void Of_NullOutput_IsNull()
    {
        var output = WorkflowsOutput.Of(MinimalStatus(output: null));
        Assert.Null(output.Output);
    }

    [Fact]
    public void Of_NonNullOutput_SerializesAsJson()
    {
        var output = WorkflowsOutput.Of(MinimalStatus(output: 99));
        Assert.Equal("99", output.Output);
    }

    [Fact]
    public void Of_NullError_IsNull()
    {
        var output = WorkflowsOutput.Of(MinimalStatus(error: null));
        Assert.Null(output.Error);
    }

    [Fact]
    public void Of_NonNullError_SerializesErrorAsJson()
    {
        var err = new ErrorResult("System.Exception", "boom", null, null, null);
        var output = WorkflowsOutput.Of(MinimalStatus(error: err));
        Assert.NotNull(output.Error);
        // Admin format: JSON-serialized ErrorResult (not "ClassName: Message" string)
        Assert.Contains("System.Exception", output.Error);
        Assert.DoesNotContain("System.Exception:", output.Error);
    }

    [Fact]
    public void Of_WithTimestamps_FormatsAsEpochMs()
    {
        var created = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var status = new WorkflowStatus(
            "wf-ts", WorkflowState.Success, "w", "C", null,
            null, null, null, null, null, null, null,
            created, created, null, null, null, null,
            null, null, null, null, null, null, null, null, null, null, null);
        var output = WorkflowsOutput.Of(status);
        var expectedMs = created.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        Assert.Equal(expectedMs, output.CreatedAt);
        Assert.Equal(expectedMs, output.UpdatedAt);
    }
}
