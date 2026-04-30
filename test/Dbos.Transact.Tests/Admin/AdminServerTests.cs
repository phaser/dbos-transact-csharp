using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Dbos.Transact.Admin;
using Dbos.Transact.Database;
using Dbos.Transact.Execution;
using Dbos.Transact.Json;
using Dbos.Transact.Migrations;
using Dbos.Transact.Sqlite.Database;
using Dbos.Transact.Tests.Fixtures;
using Dbos.Transact.Workflow;
using Dbos.Transact.Workflow.Internal;
using Microsoft.Data.Sqlite;

namespace Dbos.Transact.Tests.Admin;

/// <summary>
/// End-to-end AdminServer tests: a real <see cref="AdminServer"/> backed by a SQLite system DB,
/// hit via <see cref="HttpClient"/>. Each test owns its own port + DB so tests can run in parallel.
/// </summary>
public sealed class AdminServerTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteFixture _fixture = new(SqliteFixture.Mode.File);
    private SqliteSystemDatabase? _db;
    private DbosExecutor? _executor;
    private AdminServer? _server;
    private HttpClient? _http;

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
        await using var conn = new SqliteConnection(_fixture.ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Sqlite);
        await mgr.RunAsync();

        _db = new SqliteSystemDatabase(_fixture.ConnectionString);
        _executor = new DbosExecutor(_db, DbosJsonSerializer.Instance, executorId: "test-exec");

        var port = GetFreePort();
        _server = new AdminServer(port, _executor, _db);
        _server.Start();

        _http = new HttpClient { BaseAddress = _server.BaseUri, Timeout = TimeSpan.FromSeconds(10) };
    }

    public async Task DisposeAsync()
    {
        _http?.Dispose();
        if (_server is not null) await _server.DisposeAsync();
        if (_executor is not null) await _executor.DisposeAsync();
        if (_db is not null) await _db.DisposeAsync();
        await _fixture.DisposeAsync();
    }

    public void Dispose() { _fixture.Dispose(); GC.SuppressFinalize(this); }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private async Task<string> SeedWorkflowAsync(string name = "TestWorkflow")
    {
        var id = Guid.NewGuid().ToString();
        var status = new WorkflowStatusInternal(
            WorkflowId: id, WorkflowName: name, ClassName: "TestClass", InstanceName: null,
            QueueName: null, DeduplicationId: null, Priority: null, QueuePartitionKey: null,
            Delay: null, AuthenticatedUser: null, AssumedRole: null, AuthenticatedRoles: null,
            Inputs: null, ExecutorId: "test-exec", AppVersion: null, AppId: null,
            Timeout: null, Deadline: null, ParentWorkflowId: null, Serialization: null);
        await _db!.InitWorkflowStatusAsync(status, maxRetries: 100, isRecoveryRequest: false, isDequeuedRequest: false);
        return id;
    }

    // ── Static endpoints ──────────────────────────────────────────────────────

    [Fact]
    public async Task Healthz_Returns200WithStatus()
    {
        var resp = await _http!.GetAsync("/dbos-healthz");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"healthy\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deactivate_Returns200WithDeactivatedText()
    {
        var resp = await _http!.GetAsync("/deactivate");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("deactivated", await resp.Content.ReadAsStringAsync());
        Assert.True(_executor!.IsDeactivated);
    }

    [Fact]
    public async Task QueuesMetadata_Returns200WithInternalQueue()
    {
        var resp = await _http!.GetAsync("/dbos-workflow-queues-metadata");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task UnknownPath_Returns404()
    {
        var resp = await _http!.GetAsync("/no-such-endpoint");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ── Method / content guards ───────────────────────────────────────────────

    [Fact]
    public async Task WorkflowRecovery_GET_Returns405()
    {
        var resp = await _http!.GetAsync("/dbos-workflow-recovery");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, resp.StatusCode);
    }

    [Fact]
    public async Task WorkflowRecovery_PostWithoutJson_Returns415()
    {
        using var content = new StringContent("[\"local\"]", Encoding.UTF8, "text/plain");
        var resp = await _http!.PostAsync("/dbos-workflow-recovery", content);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, resp.StatusCode);
    }

    [Fact]
    public async Task WorkflowRecovery_EmptyExecutorList_Returns200WithEmptyArray()
    {
        var resp = await _http!.PostAsJsonAsync("/dbos-workflow-recovery", Array.Empty<string>());
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var ids = await resp.Content.ReadFromJsonAsync<string[]>();
        Assert.NotNull(ids);
        Assert.Empty(ids!);
    }

    // ── /workflows + /workflows/{id} ──────────────────────────────────────────

    [Fact]
    public async Task ListWorkflows_ReturnsAllSeeded()
    {
        var id1 = await SeedWorkflowAsync("WfOne");
        var id2 = await SeedWorkflowAsync("WfTwo");

        var resp = await _http!.PostAsJsonAsync("/workflows", new ListWorkflowsRequest(
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, null));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var ids = doc.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("WorkflowUUID").GetString())
            .ToList();
        Assert.Contains(id1, ids);
        Assert.Contains(id2, ids);
    }

    [Fact]
    public async Task GetWorkflow_ExistingId_Returns200()
    {
        var id = await SeedWorkflowAsync();
        var resp = await _http!.GetAsync($"/workflows/{id}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal(id, doc.RootElement.GetProperty("WorkflowUUID").GetString());
    }

    [Fact]
    public async Task GetWorkflow_UnknownId_Returns404()
    {
        var resp = await _http!.GetAsync("/workflows/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task ListSteps_ReturnsArray()
    {
        var id = await SeedWorkflowAsync();
        var resp = await _http!.GetAsync($"/workflows/{id}/steps");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task Cancel_Post_Returns204_AndTransitionsStatus()
    {
        var id = await SeedWorkflowAsync();
        var resp = await _http!.PostAsync($"/workflows/{id}/cancel", content: null);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var status = await _db!.GetWorkflowStatusAsync(id);
        Assert.NotNull(status);
        Assert.Equal(WorkflowState.Cancelled, status!.Status);
    }

    [Fact]
    public async Task Cancel_GET_Returns405()
    {
        var id = await SeedWorkflowAsync();
        var resp = await _http!.GetAsync($"/workflows/{id}/cancel");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, resp.StatusCode);
    }

    [Fact]
    public async Task Resume_Post_Returns204_AndTransitionsStatus()
    {
        var id = await SeedWorkflowAsync();
        await _db!.CancelWorkflowsAsync([id]);

        var resp = await _http!.PostAsync($"/workflows/{id}/resume", content: null);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var status = await _db.GetWorkflowStatusAsync(id);
        Assert.Equal(WorkflowState.Pending, status!.Status);
    }

    [Fact]
    public async Task Fork_GET_Returns405()
    {
        var id = await SeedWorkflowAsync();
        var resp = await _http!.GetAsync($"/workflows/{id}/fork");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, resp.StatusCode);
    }

    [Fact]
    public async Task Fork_PostWithoutJson_Returns415()
    {
        var id = await SeedWorkflowAsync();
        using var content = new StringContent("ignored", Encoding.UTF8, "text/plain");
        var resp = await _http!.PostAsync($"/workflows/{id}/fork", content);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, resp.StatusCode);
    }

    // Note: a full Fork success-path test is deferred until ForkWorkflowAsync is implemented in
    // the dialect DAOs (currently throws NotImplementedException on both Postgres and SQLite).
    // The endpoint routing is exercised by Fork_GET_Returns405 / Fork_PostWithoutJson_Returns415.
}
