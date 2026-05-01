using System.Text.Json;
using Dbos.Transact.Conductor;
using Dbos.Transact.Conductor.Protocol;
using Dbos.Transact.Database;
using Dbos.Transact.Execution;
using Dbos.Transact.Json;
using Dbos.Transact.Migrations;
using Dbos.Transact.Sqlite.Database;
using Dbos.Transact.Tests.Fixtures;
using Dbos.Transact.Workflow;
using Dbos.Transact.Workflow.Internal;
using Microsoft.Data.Sqlite;

namespace Dbos.Transact.Tests.Conductor;

/// <summary>
/// End-to-end Conductor tests: a real <see cref="global::Dbos.Transact.Conductor.Conductor"/>
/// dialing a <see cref="TestWebSocketServer"/> backed by a SQLite system DB.
/// </summary>
public sealed class ConductorTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteFixture _fixture = new(SqliteFixture.Mode.File);
    private SqliteSystemDatabase? _db;
    private DbosExecutor? _executor;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(10);

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
        await using var conn = new SqliteConnection(_fixture.ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Sqlite);
        await mgr.RunAsync();

        _db = new SqliteSystemDatabase(_fixture.ConnectionString);
        _executor = new DbosExecutor(_db, DbosJsonSerializer.Instance, executorId: "test-conductor-exec");
    }

    public async Task DisposeAsync()
    {
        if (_executor is not null) await _executor.DisposeAsync();
        if (_db is not null) await _db.DisposeAsync();
        await _fixture.DisposeAsync();
    }

    public void Dispose() { _fixture.Dispose(); GC.SuppressFinalize(this); }

    private global::Dbos.Transact.Conductor.Conductor BuildConductor(Uri url) =>
        new global::Dbos.Transact.Conductor.Conductor.Build(_executor!, _db!, "testapp", "testkey")
        {
            Domain = url.ToString().TrimEnd('/').Replace("/websocket/testapp/testkey", "", StringComparison.Ordinal),
            PingPeriod = TimeSpan.FromSeconds(5),
            ReconnectDelay = TimeSpan.FromMilliseconds(50),
        }.BuildConductor();

    private async Task<string> SeedWorkflowAsync()
    {
        var id = Guid.NewGuid().ToString();
        var status = new WorkflowStatusInternal(
            WorkflowId: id, WorkflowName: "TestWorkflow", ClassName: "TestClass", InstanceName: null,
            QueueName: null, DeduplicationId: null, Priority: null, QueuePartitionKey: null,
            Delay: null, AuthenticatedUser: null, AssumedRole: null, AuthenticatedRoles: null,
            Inputs: null, ExecutorId: "test-exec", AppVersion: null, AppId: null,
            Timeout: null, Deadline: null, ParentWorkflowId: null, Serialization: null);
        await _db!.InitWorkflowStatusAsync(status, maxRetries: 100, isRecoveryRequest: false, isDequeuedRequest: false);
        return id;
    }

    [Fact]
    public async Task Connects_ToServer()
    {
        await using var server = new TestWebSocketServer("/websocket/testapp/testkey");
        await using var conductor = BuildConductor(server.Url);
        conductor.Start();

        await server.ConnectedAsync.WaitAsync(ReadTimeout);

        // server.ConnectedAsync fires on the server-side accept; the conductor's background task
        // assigns _activeWebSocket on its own continuation. On slow CI runners that continuation
        // may not have been scheduled yet, so spin-wait rather than checking immediately.
        var deadline = DateTime.UtcNow + ReadTimeout;
        while (!conductor.IsConnected && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.True(conductor.IsConnected);
    }

    [Fact]
    public async Task Cancel_DispatchesAndRespondsWithSuccess()
    {
        var workflowId = await SeedWorkflowAsync();

        await using var server = new TestWebSocketServer("/websocket/testapp/testkey");
        await using var conductor = BuildConductor(server.Url);
        conductor.Start();
        await server.ConnectedAsync.WaitAsync(ReadTimeout);

        var request = new CancelRequest("req-1", workflowId);
        await server.SendAsync(JsonSerializer.Serialize<BaseMessage>(request, JsonOptions));

        var responseJson = await server.ReadMessageAsync(ReadTimeout);
        var response = JsonSerializer.Deserialize<SuccessResponse>(responseJson, JsonOptions)!;

        Assert.Equal("cancel", response.Type);
        Assert.Equal("req-1", response.RequestId);
        Assert.True(response.Success);

        var status = await _db!.GetWorkflowStatusAsync(workflowId);
        Assert.Equal(WorkflowState.Cancelled, status!.Status);
    }

    [Fact]
    public async Task ConcurrentRequests_AreCorrelatedByRequestId()
    {
        var ids = new[]
        {
            await SeedWorkflowAsync(),
            await SeedWorkflowAsync(),
            await SeedWorkflowAsync(),
        };

        await using var server = new TestWebSocketServer("/websocket/testapp/testkey");
        await using var conductor = BuildConductor(server.Url);
        conductor.Start();
        await server.ConnectedAsync.WaitAsync(ReadTimeout);

        // Send 3 cancel requests with distinct request_ids in rapid succession.
        for (var i = 0; i < ids.Length; i++)
        {
            var req = new CancelRequest($"req-{i}", ids[i]);
            await server.SendAsync(JsonSerializer.Serialize<BaseMessage>(req, JsonOptions));
        }

        var seen = new HashSet<string>();
        for (var i = 0; i < ids.Length; i++)
        {
            var json = await server.ReadMessageAsync(ReadTimeout);
            var resp = JsonSerializer.Deserialize<SuccessResponse>(json, JsonOptions)!;
            seen.Add(resp.RequestId!);
            Assert.True(resp.Success);
            Assert.Equal("cancel", resp.Type);
        }

        Assert.Equal(new HashSet<string> { "req-0", "req-1", "req-2" }, seen);
    }

    [Fact]
    public async Task ExecutorInfo_ReportsExecutorId()
    {
        await using var server = new TestWebSocketServer("/websocket/testapp/testkey");
        await using var conductor = BuildConductor(server.Url);
        conductor.Start();
        await server.ConnectedAsync.WaitAsync(ReadTimeout);

        var request = new ExecutorInfoRequest { RequestId = "info-1" };
        await server.SendAsync(JsonSerializer.Serialize<BaseMessage>(request, JsonOptions));

        var responseJson = await server.ReadMessageAsync(ReadTimeout);
        var response = JsonSerializer.Deserialize<ExecutorInfoResponse>(responseJson, JsonOptions)!;

        Assert.Equal("executor_info", response.Type);
        Assert.Equal("info-1", response.RequestId);
        Assert.Equal("test-conductor-exec", response.ExecutorId);
        Assert.Equal("csharp", response.Language);
    }

    [Fact]
    public async Task UnsupportedMessageType_ReturnsErrorResponse()
    {
        await using var server = new TestWebSocketServer("/websocket/testapp/testkey");
        await using var conductor = BuildConductor(server.Url);
        conductor.Start();
        await server.ConnectedAsync.WaitAsync(ReadTimeout);

        // RetentionRequest has no backing impl in the C# port v1.
        var request = new RetentionRequest { RequestId = "ret-1" };
        await server.SendAsync(JsonSerializer.Serialize<BaseMessage>(request, JsonOptions));

        var responseJson = await server.ReadMessageAsync(ReadTimeout);
        var response = JsonSerializer.Deserialize<BaseResponse>(responseJson, JsonOptions)!;

        Assert.Equal("retention", response.Type);
        Assert.Equal("ret-1", response.RequestId);
        Assert.False(string.IsNullOrEmpty(response.ErrorMessage));
        Assert.Contains("not implemented", response.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ServerCloses_ClientReconnects()
    {
        await using var server = new TestWebSocketServer("/websocket/testapp/testkey");
        await using var conductor = BuildConductor(server.Url);
        conductor.Start();

        await server.ConnectedAsync.WaitAsync(ReadTimeout);
        Assert.Equal(1, server.ConnectionCount);

        // Simulate the conductor service dropping the connection.
        await server.CloseConnectionAsync();

        // Wait until the client reconnects (a second accept on the test server).
        var deadline = DateTime.UtcNow + ReadTimeout;
        while (DateTime.UtcNow < deadline && server.ConnectionCount < 2)
            await Task.Delay(50);

        Assert.True(server.ConnectionCount >= 2,
            $"Expected reconnection. ConnectionCount={server.ConnectionCount}");
    }
}
