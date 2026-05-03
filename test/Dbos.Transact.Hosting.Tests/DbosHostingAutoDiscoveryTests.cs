using Dbos.Transact;
using Dbos.Transact.Hosting;
using Dbos.Transact.Hosting.Tests.AutoDiscoveryFixtures;
using Dbos.Transact.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dbos.Transact.Hosting.Tests;

/// <summary>
/// Tests for <c>AddDbosWorkflowsFromAssembly</c>.
/// Each test owns a unique temp SQLite DB for isolation.
/// </summary>
public sealed class DbosHostingAutoDiscoveryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;

    public DbosHostingAutoDiscoveryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"dbos_autodiscovery_{Guid.NewGuid():N}.sqlite");
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

    [Fact]
    public async Task FromAssembly_BootsAndResolvesProxies()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddDbos("auto-asm", b => b.UseSqlite(_connectionString));
        builder.Services.AddDbosWorkflowsFromAssembly(typeof(AutoDiscoveryWorkflow).Assembly);

        using var host = builder.Build();
        await host.StartAsync();

        Assert.NotNull(host.Services.GetRequiredService<IAutoDiscoveryStep>());
        Assert.NotNull(host.Services.GetRequiredService<IAutoDiscoveryWorkflow>());

        await host.StopAsync();
    }

    [Fact]
    public async Task FromAssembly_WorkflowRunsToCompletion()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddDbos("auto-asm-run", b => b.UseSqlite(_connectionString));
        builder.Services.AddDbosWorkflowsFromAssembly(typeof(AutoDiscoveryWorkflow).Assembly);

        using var host = builder.Build();
        await host.StartAsync();

        var dbos = host.Services.GetRequiredService<Dbos>();
        var handle = await dbos.StartWorkflowAsync<string>(
            workflowName: nameof(AutoDiscoveryWorkflow.RunAsync),
            className: typeof(AutoDiscoveryWorkflow).FullName,
            instanceName: null,
            args: ["hello"]);

        Assert.Equal("hello", await handle.GetResultAsync());

        await host.StopAsync();
    }

    [Fact]
    public async Task FromAssembly_UnannotatedTypeNotRegistered()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddDbos("auto-asm-plain", b => b.UseSqlite(_connectionString));
        builder.Services.AddDbosWorkflowsFromAssembly(typeof(AutoDiscoveryPlain).Assembly);

        using var host = builder.Build();
        await host.StartAsync();

        Assert.Null(host.Services.GetService<IAutoDiscoveryPlain>());

        await host.StopAsync();
    }
}
