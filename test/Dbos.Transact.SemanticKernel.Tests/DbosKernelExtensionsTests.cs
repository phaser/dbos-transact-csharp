using System.ComponentModel;
using Dbos.Transact.Migrations;
using Dbos.Transact.Postgres;
using Dbos.Transact.Sqlite;
using Dbos.Transact.Tests.Fixtures;
using Dbos.Transact.Workflow;
using Microsoft.Data.Sqlite;
using Microsoft.SemanticKernel;
using Npgsql;

namespace Dbos.Transact.SemanticKernel.Tests;

#pragma warning disable CA1812 // proxied/instantiated via reflection
file interface IWeatherTools
{
    [KernelFunction]
    [Description("Get the weather for a city.")]
    [Step]
    Task<string> GetWeatherAsync(string city);
}

file sealed class WeatherTools : IWeatherTools
{
    public int InvocationCount { get; private set; }

    public Task<string> GetWeatherAsync(string city)
    {
        // Capture context state at the moment of invocation — proves the call
        // travelled through the DBOS proxy + step interceptor.
        ObservedInWorkflow = Dbos.InWorkflow();
        ObservedInStep = Dbos.InStep();
        ObservedStepId = Dbos.StepId();
        InvocationCount++;
        return Task.FromResult($"Sunny in {city}");
    }

    public bool ObservedInWorkflow { get; private set; }
    public bool ObservedInStep { get; private set; }
    public int? ObservedStepId { get; private set; }
}

file interface ISkAgentWorkflow
{
    Task<string> RunAsync(string city);
}

file sealed class SkAgentWorkflow : ISkAgentWorkflow
{
    private readonly Kernel _kernel;

    public SkAgentWorkflow(Kernel kernel) => _kernel = kernel;

    [Workflow]
    public async Task<string> RunAsync(string city)
    {
        var args = new KernelArguments { ["city"] = city };
        var result = await _kernel.InvokeAsync("Weather", "GetWeather", args).ConfigureAwait(false);
        return result.GetValue<string>() ?? string.Empty;
    }
}
#pragma warning restore CA1812

file static class SkScenarios
{
    public static async Task PluginToolCallIsCheckpointed(DbosBuilder builder)
    {
        var tools = new WeatherTools();
        var kernel = Kernel.CreateBuilder().Build();

        await using var dbos = builder
            .WithOptions(o => o with { ExecutorId = "dbos-sk-test" })
            .Build();

        kernel.AddDbosPlugin<IWeatherTools>(dbos, tools, pluginName: "Weather");
        dbos.RegisterProxy<ISkAgentWorkflow>(new SkAgentWorkflow(kernel));

        await dbos.LaunchAsync();

        var handle = await dbos.StartWorkflowAsync<string>(
            workflowName: nameof(SkAgentWorkflow.RunAsync),
            className: typeof(SkAgentWorkflow).FullName,
            instanceName: null,
            args: ["Boston"]);

        var result = await handle.GetResultAsync();
        Assert.Equal("Sunny in Boston", result);
        Assert.Equal(1, tools.InvocationCount);

        // The tool ran inside the DBOS step interceptor — proves the SK plugin
        // dispatched through the proxy rather than calling the impl directly.
        Assert.True(tools.ObservedInWorkflow);
        Assert.True(tools.ObservedInStep);
        Assert.Equal(0, tools.ObservedStepId);

        // The step is durably recorded — exactly one operation_outputs row,
        // with the method name surfaced as FunctionName.
        var steps = await dbos.ListWorkflowStepsAsync(handle.WorkflowId);
        Assert.Single(steps);
        Assert.Equal(0, steps[0].FunctionId);
        Assert.Equal(nameof(WeatherTools.GetWeatherAsync), steps[0].FunctionName);
        Assert.NotNull(steps[0].Output);
    }

    public static async Task AddDbosPlugin_RejectsNonInterface(DbosBuilder builder)
    {
        await using var dbos = builder.Build();
        var kernel = Kernel.CreateBuilder().Build();

        Assert.Throws<ArgumentException>(() =>
            kernel.AddDbosPlugin<WeatherTools>(dbos, new WeatherTools()));
    }
}

[Collection("Postgres")]
public sealed class PostgresDbosKernelExtensionsTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string Schema = "dbos_sk_pg_test";
    private readonly PostgresFixture _fixture;

    public PostgresDbosKernelExtensionsTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Postgres, Schema);
        await mgr.RunAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private DbosBuilder NewBuilder() =>
        Dbos.Builder("dbos-sk-pg-test")
            .WithOptions(o => o with { Migrate = false })
            .UsePostgres(_fixture.ConnectionString, Schema);

    [Fact] public Task PluginToolCallIsCheckpointed() => SkScenarios.PluginToolCallIsCheckpointed(NewBuilder());
    [Fact] public Task AddDbosPlugin_RejectsNonInterface() => SkScenarios.AddDbosPlugin_RejectsNonInterface(NewBuilder());
}

public sealed class SqliteDbosKernelExtensionsTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteFixture _fixture = new(SqliteFixture.Mode.File);

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
        await using var conn = new SqliteConnection(_fixture.ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Sqlite);
        await mgr.RunAsync();
    }

    public Task DisposeAsync() => _fixture.DisposeAsync();
    public void Dispose() { _fixture.Dispose(); GC.SuppressFinalize(this); }

    private DbosBuilder NewBuilder() =>
        Dbos.Builder("dbos-sk-sqlite-test")
            .WithOptions(o => o with { Migrate = false })
            .UseSqlite(_fixture.ConnectionString);

    [Fact] public Task PluginToolCallIsCheckpointed() => SkScenarios.PluginToolCallIsCheckpointed(NewBuilder());
    [Fact] public Task AddDbosPlugin_RejectsNonInterface() => SkScenarios.AddDbosPlugin_RejectsNonInterface(NewBuilder());
}
