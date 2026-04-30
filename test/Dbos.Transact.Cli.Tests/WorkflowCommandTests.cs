using System.Text.Json;
using Dbos.Transact.Migrations;
using Dbos.Transact.Sqlite.Database;
using Dbos.Transact.Workflow;
using Dbos.Transact.Workflow.Internal;
using Microsoft.Data.Sqlite;

namespace Dbos.Transact.Cli.Tests;

public sealed class WorkflowCommandTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteFixture _fixture = new();

    public async Task InitializeAsync()
    {
        await using var conn = new SqliteConnection(_fixture.ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Sqlite);
        await mgr.RunAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() { _fixture.Dispose(); GC.SuppressFinalize(this); }

    /// <summary>Seeds a PENDING workflow row directly via SystemDatabase, bypassing the executor.</summary>
    private async Task<string> SeedWorkflowAsync(string nameOverride = "TestWorkflow")
    {
        await using var db = new SqliteSystemDatabase(_fixture.ConnectionString);
        var id = Guid.NewGuid().ToString();
        var status = new WorkflowStatusInternal(
            WorkflowId: id,
            WorkflowName: nameOverride,
            ClassName: "TestClass",
            InstanceName: null,
            QueueName: null,
            DeduplicationId: null,
            Priority: null,
            QueuePartitionKey: null,
            Delay: null,
            AuthenticatedUser: null,
            AssumedRole: null,
            AuthenticatedRoles: null,
            Inputs: null,
            ExecutorId: "test-exec",
            AppVersion: null,
            AppId: null,
            Timeout: null,
            Deadline: null,
            ParentWorkflowId: null,
            Serialization: null);
        await db.InitWorkflowStatusAsync(status, maxRetries: 100, isRecoveryRequest: false, isDequeuedRequest: false);
        return id;
    }

    [Fact]
    public async Task Workflow_List_ReturnsSeededWorkflowsAsJson()
    {
        var id1 = await SeedWorkflowAsync("WfOne");
        var id2 = await SeedWorkflowAsync("WfTwo");

        var r = await CliRunner.RunAsync(
            "workflow", "list",
            "--db-url", _fixture.ConnectionString);
        Assert.True(r.ExitCode == 0, $"stderr={r.Stderr}");

        using var doc = JsonDocument.Parse(r.Stdout);
        var ids = doc.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("workflowId").GetString())
            .ToList();
        Assert.Contains(id1, ids);
        Assert.Contains(id2, ids);
    }

    [Fact]
    public async Task Workflow_Get_ReturnsSpecificWorkflow()
    {
        var id = await SeedWorkflowAsync();
        var r = await CliRunner.RunAsync(
            "workflow", "get", id,
            "--db-url", _fixture.ConnectionString);
        Assert.True(r.ExitCode == 0, $"stderr={r.Stderr}");

        using var doc = JsonDocument.Parse(r.Stdout);
        Assert.Equal(id, doc.RootElement.GetProperty("workflowId").GetString());
    }

    [Fact]
    public async Task Workflow_Get_UnknownId_ExitsNonZero()
    {
        var r = await CliRunner.RunAsync(
            "workflow", "get", "does-not-exist",
            "--db-url", _fixture.ConnectionString);
        Assert.NotEqual(0, r.ExitCode);
        Assert.Contains("Failed to retrieve workflow", r.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Workflow_Cancel_TransitionsStatusToCancelled()
    {
        var id = await SeedWorkflowAsync();

        var r = await CliRunner.RunAsync(
            "workflow", "cancel", id,
            "--db-url", _fixture.ConnectionString);
        Assert.True(r.ExitCode == 0, $"stderr={r.Stderr}");
        Assert.Contains("Successfully cancelled", r.Stdout, StringComparison.Ordinal);

        await using var db = new SqliteSystemDatabase(_fixture.ConnectionString);
        var status = await db.GetWorkflowStatusAsync(id);
        Assert.NotNull(status);
        Assert.Equal(WorkflowState.Cancelled, status!.Status);
    }

    [Fact]
    public async Task Workflow_Resume_AfterCancel_TransitionsBackToPending()
    {
        var id = await SeedWorkflowAsync();

        var cancelResult = await CliRunner.RunAsync(
            "workflow", "cancel", id,
            "--db-url", _fixture.ConnectionString);
        Assert.Equal(0, cancelResult.ExitCode);

        var r = await CliRunner.RunAsync(
            "workflow", "resume", id,
            "--db-url", _fixture.ConnectionString);
        Assert.True(r.ExitCode == 0, $"stderr={r.Stderr}");

        await using var db = new SqliteSystemDatabase(_fixture.ConnectionString);
        var status = await db.GetWorkflowStatusAsync(id);
        Assert.NotNull(status);
        // Non-queued workflows resume to PENDING so an executor can re-pick them.
        Assert.Equal(WorkflowState.Pending, status!.Status);
    }
}
