using System.ComponentModel;
using System.Runtime.CompilerServices;
using Dbos.Transact.Hosting;
using Dbos.Transact.Migrations;
using Dbos.Transact.Postgres;
using Dbos.Transact.SemanticKernel.Hosting;
using Dbos.Transact.Sqlite;
using Dbos.Transact.Tests.Fixtures;
using Dbos.Transact.Workflow;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Npgsql;

namespace Dbos.Transact.SemanticKernel.Tests.Hosting;

#pragma warning disable CA1812 // proxied/instantiated via reflection / DI
file sealed class FakeWeatherChatService : IChatCompletionService
{
    public int InvocationCount { get; private set; }
    public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

    public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        InvocationCount++;
        IReadOnlyList<ChatMessageContent> result =
        [
            new ChatMessageContent(AuthorRole.Assistant, "ack"),
        ];
        return Task.FromResult(result);
    }

    public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default) =>
        EmptyStream(cancellationToken);

    private static async IAsyncEnumerable<StreamingChatMessageContent> EmptyStream(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        yield break;
    }
}

file interface IWeatherTools
{
    [KernelFunction, Description("Get the weather for a city.")]
    [Step]
    Task<string> GetWeatherAsync(string city);
}

file sealed class WeatherTools : IWeatherTools
{
    public int InvocationCount { get; private set; }
    public Task<string> GetWeatherAsync(string city)
    {
        InvocationCount++;
        return Task.FromResult($"Sunny in {city}");
    }
}

file interface IHostedAgentWorkflow
{
    Task<string> RunAsync(string city);
}

file sealed class HostedAgentWorkflow : IHostedAgentWorkflow
{
    private readonly Kernel _kernel;
    private readonly IDurableChatCompletionService _chat;

    public HostedAgentWorkflow(Kernel kernel, IDurableChatCompletionService chat)
    {
        _kernel = kernel;
        _chat = chat;
    }

    [Workflow]
    public async Task<string> RunAsync(string city)
    {
        // Both injected dependencies are hot-wired by the pre-launch configurators.
        // Each call below is a top-level [Step] checkpointed by DBOS.
        var llm = await _chat.CompleteAsync([new("user", $"weather in {city}?")]).ConfigureAwait(false);
        var tool = await _kernel.InvokeAsync("Weather", "GetWeather", new() { ["city"] = city }).ConfigureAwait(false);
        return $"{llm.Content}|{tool.GetValue<string>()}";
    }
}
#pragma warning restore CA1812

file static class HostedScenarios
{
    public static async Task BothInjectedDependenciesAreWiredAndCheckpointed(
        Action<IServiceCollection> configureDbos)
    {
        var fakeChat = new FakeWeatherChatService();
        var weatherTools = new WeatherTools();

        var hostBuilder = Host.CreateApplicationBuilder();
        hostBuilder.Services.AddSingleton<IChatCompletionService>(fakeChat);
        hostBuilder.Services.AddSingleton<WeatherTools>(weatherTools);
        hostBuilder.Services.AddSingleton(sp =>
        {
            var builder = Kernel.CreateBuilder();
            builder.Services.AddSingleton(sp.GetRequiredService<IChatCompletionService>());
            return builder.Build();
        });

        configureDbos(hostBuilder.Services);

        hostBuilder.Services.AddDbosSemanticKernelPlugin<IWeatherTools, WeatherTools>(pluginName: "Weather");
        hostBuilder.Services.AddDbosDurableChatCompletion();
        hostBuilder.Services.AddDbosWorkflow<IHostedAgentWorkflow, HostedAgentWorkflow>();

        using var host = hostBuilder.Build();
        await host.StartAsync().ConfigureAwait(false);
        try
        {
            var dbos = host.Services.GetRequiredService<Dbos>();
            var handle = await dbos.StartWorkflowAsync<string>(
                workflowName: nameof(HostedAgentWorkflow.RunAsync),
                className: typeof(HostedAgentWorkflow).FullName,
                instanceName: null,
                args: ["Boston"]);

            var result = await handle.GetResultAsync();
            Assert.Equal("ack|Sunny in Boston", result);
            Assert.Equal(1, fakeChat.InvocationCount);
            Assert.Equal(1, weatherTools.InvocationCount);

            // Both calls were recorded as separate steps via the proxy interceptor —
            // the configurators wired everything before workflow registration.
            var steps = await dbos.ListWorkflowStepsAsync(handle.WorkflowId);
            Assert.Equal(2, steps.Count);
            Assert.Equal(nameof(IDurableChatCompletionService.CompleteAsync), steps[0].FunctionName);
            Assert.Equal(nameof(WeatherTools.GetWeatherAsync), steps[1].FunctionName);
        }
        finally
        {
            await host.StopAsync().ConfigureAwait(false);
        }
    }
}

[Collection("Postgres")]
public sealed class PostgresHostedAgentTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string Schema = "dbos_sk_host_pg_test";
    private readonly PostgresFixture _fixture;

    public PostgresHostedAgentTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Postgres, Schema);
        await mgr.RunAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public Task BothInjectedDependenciesAreWiredAndCheckpointed() =>
        HostedScenarios.BothInjectedDependenciesAreWiredAndCheckpointed(services =>
            services.AddDbos("dbos-sk-host-pg-test", builder => builder
                .WithOptions(o => o with { Migrate = false })
                .UsePostgres(_fixture.ConnectionString, Schema)));
}

public sealed class SqliteHostedAgentTests : IAsyncLifetime, IDisposable
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

    [Fact]
    public Task BothInjectedDependenciesAreWiredAndCheckpointed() =>
        HostedScenarios.BothInjectedDependenciesAreWiredAndCheckpointed(services =>
            services.AddDbos("dbos-sk-host-sqlite-test", builder => builder
                .WithOptions(o => o with { Migrate = false })
                .UseSqlite(_fixture.ConnectionString)));
}
