using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Dbos.Transact.Json;
using Dbos.Transact.Migrations;
using Dbos.Transact.Sqlite;
using Dbos.Transact.Sqlite.Database;
using Dbos.Transact.Tests.Fixtures;
using Dbos.Transact.Workflow;
using Dbos.Transact.Workflow.Internal;
using Microsoft.Data.Sqlite;

namespace Dbos.Transact.Tests;

// ── Test workflow types ───────────────────────────────────────────────────────

#pragma warning disable CA1812
file interface IAdminTestWorkflow
{
    Task RunAsync();
}

[WorkflowClassName("AdminRecoveryTestWf")]
file sealed class AdminTestWorkflow : IAdminTestWorkflow
{
    [Workflow]
    public Task RunAsync() => Task.CompletedTask;
}
#pragma warning restore CA1812

// ── Tests ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Verifies that <see cref="Dbos"/> starts and stops <see cref="Admin.AdminServer"/> when
/// <see cref="DbosOptions.AdminServer"/> is enabled, and that the recovery endpoint is wired.
/// </summary>
public sealed class DbosAdminServerTests : IAsyncLifetime, IDisposable
{
    private const string ExecutorId = "live-exec-admin-test";
    private const string DeadExecutorId = "dead-exec-admin-test";

    private readonly SqliteFixture _fixture = new(SqliteFixture.Mode.File);
    private SqliteSystemDatabase? _seedDb;
    private int _port;

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
        await using var conn = new SqliteConnection(_fixture.ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Sqlite);
        await mgr.RunAsync();

        _seedDb = new SqliteSystemDatabase(_fixture.ConnectionString);
        await _seedDb.StartAsync();

        _port = GetFreePort();
    }

    public async Task DisposeAsync()
    {
        if (_seedDb is not null) await _seedDb.DisposeAsync();
        await _fixture.DisposeAsync();
    }

    public void Dispose() { _fixture.Dispose(); GC.SuppressFinalize(this); }

    private Dbos BuildDbos() =>
        Dbos.Builder("admin-server-test")
            .WithOptions(o => o with
            {
                Migrate = false,
                ExecutorId = ExecutorId,
                AdminServer = true,
                AdminServerPort = _port,
            })
            .UseSqlite(_fixture.ConnectionString)
            .Build();

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private async Task<string> SeedPendingWorkflowAsync()
    {
        var id = Guid.NewGuid().ToString();
        var status = new WorkflowStatusInternal(
            WorkflowId: id,
            WorkflowName: nameof(AdminTestWorkflow.RunAsync),
            ClassName: "AdminRecoveryTestWf",
            InstanceName: null,
            QueueName: null, DeduplicationId: null, Priority: null, QueuePartitionKey: null,
            Delay: null, AuthenticatedUser: null, AssumedRole: null, AuthenticatedRoles: null,
            Inputs: null,
            ExecutorId: DeadExecutorId,
            AppVersion: null, AppId: null, Timeout: null, Deadline: null,
            ParentWorkflowId: null, Serialization: DbosJsonSerializer.Instance.Name);
        await _seedDb!.InitWorkflowStatusAsync(
            status, maxRetries: 100, isRecoveryRequest: false, isDequeuedRequest: false);
        return id;
    }

    [Fact]
    public async Task AdminServer_StartsAfterLaunch()
    {
        await using var dbos = BuildDbos();
        dbos.RegisterProxy<IAdminTestWorkflow>(new AdminTestWorkflow());
        await dbos.LaunchAsync();

        using var http = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{_port}/"),
            Timeout = TimeSpan.FromSeconds(5),
        };
        var resp = await http.GetAsync("/dbos-healthz");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task AdminServer_StopsAfterShutdown()
    {
        await using var dbos = BuildDbos();
        dbos.RegisterProxy<IAdminTestWorkflow>(new AdminTestWorkflow());
        await dbos.LaunchAsync();
        await dbos.ShutdownAsync();

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        await Assert.ThrowsAnyAsync<HttpRequestException>(
            () => http.GetAsync($"http://127.0.0.1:{_port}/dbos-healthz"));
    }

    [Fact]
    public async Task RecoveryEndpoint_ReturnsPendingWorkflowId()
    {
        await using var dbos = BuildDbos();
        dbos.RegisterProxy<IAdminTestWorkflow>(new AdminTestWorkflow());
        await dbos.LaunchAsync();

        var workflowId = await SeedPendingWorkflowAsync();

        using var http = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{_port}/"),
            Timeout = TimeSpan.FromSeconds(10),
        };
        var resp = await http.PostAsJsonAsync("/dbos-workflow-recovery", new[] { DeadExecutorId });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var ids = await resp.Content.ReadFromJsonAsync<string[]>();
        Assert.NotNull(ids);
        Assert.Contains(workflowId, ids);
    }
}
