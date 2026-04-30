using Dbos.Transact;
using Dbos.Transact.Hosting;
using Dbos.Transact.Sqlite;
using Dbos.Transact.Workflow;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dbos.Transact.Hosting.Tests;

#pragma warning disable CA1812 // proxied via Castle.DynamicProxy

file interface IHostingTestStepService
{
    [Step]
    Task<int> AddOneAsync(int value);
}

file sealed class HostingTestStepService : IHostingTestStepService
{
    public int InvocationCount { get; private set; }

    public Task<int> AddOneAsync(int value)
    {
        InvocationCount++;
        return Task.FromResult(value + 1);
    }
}

file interface IHostingTestWorkflowService
{
    Task<int> RunAsync(int n);
}

file sealed class HostingTestWorkflowService : IHostingTestWorkflowService
{
    private readonly IHostingTestStepService _steps;

    public HostingTestWorkflowService(IHostingTestStepService steps) => _steps = steps;

    [Workflow]
    public Task<int> RunAsync(int n) => _steps.AddOneAsync(n);
}

#pragma warning restore CA1812

/// <summary>
/// Tests that boot the hosting integration against a SQLite file fixture.
/// Each test owns a unique temp DB so they can run in parallel.
/// </summary>
public sealed class DbosHostingExtensionsTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;

    public DbosHostingExtensionsTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"dbos_hosting_{Guid.NewGuid():N}.sqlite");
        _connectionString = $"Data Source={_dbPath};";
    }

    public void Dispose()
    {
        TryDelete(_dbPath);
        TryDelete(_dbPath + "-wal");
        TryDelete(_dbPath + "-shm");
        GC.SuppressFinalize(this);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
    }

    private IHost BuildHost(Action<IServiceCollection>? extra = null, Action<DbosOptionsConfigurator>? configureOptions = null)
    {
        var builder = Host.CreateApplicationBuilder();
        var cs = _connectionString;

        builder.Services.AddDbos("hosting-test", b => b.UseSqlite(cs));
        if (configureOptions is not null)
            builder.Services.Configure(configureOptions);

        builder.Services.AddDbosWorkflow<IHostingTestStepService, HostingTestStepService>();
        builder.Services.AddDbosWorkflow<IHostingTestWorkflowService, HostingTestWorkflowService>();
        extra?.Invoke(builder.Services);
        return builder.Build();
    }

    [Fact]
    public async Task HostBuilder_BootsDbosAgainstSqlite()
    {
        using var host = BuildHost();

        await host.StartAsync();

        var dbos = host.Services.GetRequiredService<Dbos>();
        Assert.True(dbos.IsLaunched);

        await host.StopAsync();
        Assert.False(dbos.IsLaunched);
    }

    [Fact]
    public async Task RegisteredWorkflow_RunsToCompletion()
    {
        using var host = BuildHost();
        await host.StartAsync();

        var dbos = host.Services.GetRequiredService<Dbos>();

        // Resolving via DI returns the proxy; that proves the AddDbosWorkflow factory ran.
        var stepProxy = host.Services.GetRequiredService<IHostingTestStepService>();
        Assert.NotNull(stepProxy);
        var workflowProxy = host.Services.GetRequiredService<IHostingTestWorkflowService>();
        Assert.NotNull(workflowProxy);

        var handle = await dbos.StartWorkflowAsync<int>(
            workflowName: nameof(HostingTestWorkflowService.RunAsync),
            className: typeof(HostingTestWorkflowService).FullName,
            instanceName: null,
            args: [21]);

        Assert.Equal(22, await handle.GetResultAsync());

        await host.StopAsync();
    }

    [Fact]
    public async Task GracefulShutdown_DisposesDbosBeforeProcessExits()
    {
        var host = BuildHost();
        await host.StartAsync();

        var dbos = host.Services.GetRequiredService<Dbos>();
        Assert.True(dbos.IsLaunched);

        // Start a workflow and let it finish before stopping the host.
        var handle = await dbos.StartWorkflowAsync<int>(
            workflowName: nameof(HostingTestWorkflowService.RunAsync),
            className: typeof(HostingTestWorkflowService).FullName,
            instanceName: null,
            args: [99]);
        Assert.Equal(100, await handle.GetResultAsync());

        await host.StopAsync();
        Assert.False(dbos.IsLaunched);

        // After shutdown, calls into the facade should fail rather than silently no-op.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dbos.GetWorkflowStatusAsync(handle.WorkflowId));

        host.Dispose();
    }

    [Fact]
    public async Task Configurator_BindsFromIConfiguration()
    {
        var builder = Host.CreateApplicationBuilder();
        var cs = _connectionString;

        var dict = new Dictionary<string, string?>
        {
            ["Dbos:Application:Name"] = "from-config",
            ["Dbos:Application:Version"] = "0.0.1",
            ["Dbos:ExecutorId"] = "exec-from-config",
            ["Dbos:Datasource:Migrate"] = "true",
        };
        builder.Configuration.AddInMemoryCollection(dict);

        builder.Services.Configure<DbosOptionsConfigurator>(builder.Configuration.GetSection("Dbos"));
        builder.Services.AddDbos(b => b.UseSqlite(cs));

        using var host = builder.Build();
        await host.StartAsync();

        var dbos = host.Services.GetRequiredService<Dbos>();
        Assert.Equal("from-config", dbos.Options.AppName);
        Assert.Equal("0.0.1", dbos.Options.AppVersion);
        Assert.Equal("exec-from-config", dbos.Options.ExecutorId);

        var configurator = host.Services.GetRequiredService<IOptions<DbosOptionsConfigurator>>().Value;
        Assert.Equal("from-config", configurator.Application.Name);

        await host.StopAsync();
    }

    [Fact]
    public async Task AddDbosQueue_RegistersQueueBeforeLaunch()
    {
        var queue = new Queue("hosting-queue").WithConcurrency(2);

        using var host = BuildHost(s => s.AddDbosQueue(queue));
        await host.StartAsync();

        var dbos = host.Services.GetRequiredService<Dbos>();
        var resolved = dbos.GetQueue("hosting-queue");
        Assert.NotNull(resolved);
        Assert.Equal(2, resolved!.Concurrency);

        await host.StopAsync();
    }
}
