using Dbos.Transact;
using Dbos.Transact.Hosting;
using Dbos.Transact.Sqlite;
using Dbos.Transact.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// These types are public so GetExportedTypes() can see them during assembly-scan tests.
#pragma warning disable CA1812 // proxied via Castle.DynamicProxy

namespace Dbos.Transact.Hosting.Tests.AutoDiscoveryFixtures
{
    public interface IAutoDiscoveryStep
    {
        [Step]
        Task<string> EchoAsync(string value);
    }

    public sealed class AutoDiscoveryStep : IAutoDiscoveryStep
    {
        public Task<string> EchoAsync(string value) => Task.FromResult(value);
    }

    public interface IAutoDiscoveryWorkflow
    {
        Task<string> RunAsync(string value);
    }

    public sealed class AutoDiscoveryWorkflow : IAutoDiscoveryWorkflow
    {
        private readonly IAutoDiscoveryStep _steps;
        public AutoDiscoveryWorkflow(IAutoDiscoveryStep steps) => _steps = steps;

        [Workflow]
        public Task<string> RunAsync(string value) => _steps.EchoAsync(value);
    }

    // Not annotated — must NOT be registered by auto-discovery.
    public interface IAutoDiscoveryPlain
    {
        Task DoSomethingAsync();
    }

    public sealed class AutoDiscoveryPlain : IAutoDiscoveryPlain
    {
        public Task DoSomethingAsync() => Task.CompletedTask;
    }
}

#pragma warning restore CA1812

namespace Dbos.Transact.Hosting.Tests
{

/// <summary>
/// Tests for <c>AddDbosWorkflowsFromAssembly</c> and <c>AddDbosWorkflowsFromServices</c>.
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

    // ── AddDbosWorkflowsFromAssembly ──────────────────────────────────────────

    [Fact]
    public async Task FromAssembly_BootsAndResolvesProxies()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddDbos("auto-asm", b => b.UseSqlite(_connectionString));
        builder.Services.AddDbosWorkflowsFromAssembly(typeof(AutoDiscoveryFixtures.AutoDiscoveryWorkflow).Assembly);

        using var host = builder.Build();
        await host.StartAsync();

        var step = host.Services.GetRequiredService<AutoDiscoveryFixtures.IAutoDiscoveryStep>();
        var workflow = host.Services.GetRequiredService<AutoDiscoveryFixtures.IAutoDiscoveryWorkflow>();

        Assert.NotNull(step);
        Assert.NotNull(workflow);

        await host.StopAsync();
    }

    [Fact]
    public async Task FromAssembly_WorkflowRunsToCompletion()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddDbos("auto-asm-run", b => b.UseSqlite(_connectionString));
        builder.Services.AddDbosWorkflowsFromAssembly(typeof(AutoDiscoveryFixtures.AutoDiscoveryWorkflow).Assembly);

        using var host = builder.Build();
        await host.StartAsync();

        var dbos = host.Services.GetRequiredService<Dbos>();
        var handle = await dbos.StartWorkflowAsync<string>(
            workflowName: nameof(AutoDiscoveryFixtures.AutoDiscoveryWorkflow.RunAsync),
            className: typeof(AutoDiscoveryFixtures.AutoDiscoveryWorkflow).FullName,
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
        builder.Services.AddDbosWorkflowsFromAssembly(typeof(AutoDiscoveryFixtures.AutoDiscoveryPlain).Assembly);

        using var host = builder.Build();
        await host.StartAsync();

        // Plain (unannotated) type should not be resolvable as a Dbos proxy.
        var plain = host.Services.GetService<AutoDiscoveryFixtures.IAutoDiscoveryPlain>();
        Assert.Null(plain);

        await host.StopAsync();
    }

    // ── AddDbosWorkflowsFromServices ──────────────────────────────────────────

    [Fact]
    public async Task FromServices_DiscoversPreviouslyRegisteredWorkflows()
    {
        var builder = Host.CreateApplicationBuilder();
        // Register the implementation under its interface as a normal DI service first.
        builder.Services.AddSingleton<AutoDiscoveryFixtures.IAutoDiscoveryStep, AutoDiscoveryFixtures.AutoDiscoveryStep>();
        builder.Services.AddSingleton<AutoDiscoveryFixtures.IAutoDiscoveryWorkflow, AutoDiscoveryFixtures.AutoDiscoveryWorkflow>();

        builder.Services.AddDbos("auto-svc", b => b.UseSqlite(_connectionString));
        builder.Services.AddDbosWorkflowsFromServices();

        using var host = builder.Build();
        await host.StartAsync();

        var step = host.Services.GetRequiredService<AutoDiscoveryFixtures.IAutoDiscoveryStep>();
        var workflow = host.Services.GetRequiredService<AutoDiscoveryFixtures.IAutoDiscoveryWorkflow>();

        Assert.NotNull(step);
        Assert.NotNull(workflow);

        await host.StopAsync();
    }

    [Fact]
    public async Task FromServices_WorkflowRunsToCompletion()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<AutoDiscoveryFixtures.IAutoDiscoveryStep, AutoDiscoveryFixtures.AutoDiscoveryStep>();
        builder.Services.AddSingleton<AutoDiscoveryFixtures.IAutoDiscoveryWorkflow, AutoDiscoveryFixtures.AutoDiscoveryWorkflow>();

        builder.Services.AddDbos("auto-svc-run", b => b.UseSqlite(_connectionString));
        builder.Services.AddDbosWorkflowsFromServices();

        using var host = builder.Build();
        await host.StartAsync();

        var dbos = host.Services.GetRequiredService<Dbos>();
        var handle = await dbos.StartWorkflowAsync<string>(
            workflowName: nameof(AutoDiscoveryFixtures.AutoDiscoveryWorkflow.RunAsync),
            className: typeof(AutoDiscoveryFixtures.AutoDiscoveryWorkflow).FullName,
            instanceName: null,
            args: ["world"]);

        Assert.Equal("world", await handle.GetResultAsync());

        await host.StopAsync();
    }

    [Fact]
    public async Task FromServices_UnannotatedTypeNotRegisteredAsDbosSingleton()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<AutoDiscoveryFixtures.IAutoDiscoveryPlain, AutoDiscoveryFixtures.AutoDiscoveryPlain>();

        builder.Services.AddDbos("auto-svc-plain", b => b.UseSqlite(_connectionString));
        builder.Services.AddDbosWorkflowsFromServices();

        using var host = builder.Build();
        await host.StartAsync();

        // The plain service was registered, so it should resolve — but as the raw impl, not a Dbos proxy.
        var plain = host.Services.GetService<AutoDiscoveryFixtures.IAutoDiscoveryPlain>();
        Assert.NotNull(plain);
        // No DbosWorkflowRegistration should exist for it.
        var registrations = host.Services.GetServices<DbosWorkflowRegistration>();
        Assert.DoesNotContain(registrations, r => r.InterfaceType == typeof(AutoDiscoveryFixtures.IAutoDiscoveryPlain));

        await host.StopAsync();
    }

    [Fact]
    public async Task FromServices_DoesNotDoubleRegisterExplicitWorkflows()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<AutoDiscoveryFixtures.IAutoDiscoveryStep, AutoDiscoveryFixtures.AutoDiscoveryStep>();

        builder.Services.AddDbos("auto-svc-nodup", b => b.UseSqlite(_connectionString));
        // Explicit registration first.
        builder.Services.AddDbosWorkflow<AutoDiscoveryFixtures.IAutoDiscoveryStep, AutoDiscoveryFixtures.AutoDiscoveryStep>();
        // Auto-discovery should skip the already-registered type.
        builder.Services.AddDbosWorkflowsFromServices();

        using var host = builder.Build();
        await host.StartAsync();

        var registrations = host.Services.GetServices<DbosWorkflowRegistration>().ToList();
        var stepCount = registrations.Count(r => r.InterfaceType == typeof(AutoDiscoveryFixtures.IAutoDiscoveryStep));
        Assert.Equal(1, stepCount);

        await host.StopAsync();
    }
}
} // namespace Dbos.Transact.Hosting.Tests
