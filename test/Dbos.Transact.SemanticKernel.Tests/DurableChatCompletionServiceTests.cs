using System.Runtime.CompilerServices;
using Dbos.Transact.Migrations;
using Dbos.Transact.Postgres;
using Dbos.Transact.Sqlite;
using Dbos.Transact.Tests.Fixtures;
using Dbos.Transact.Workflow;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Npgsql;

namespace Dbos.Transact.SemanticKernel.Tests;

#pragma warning disable CA1812 // proxied/instantiated via reflection
file sealed class CountingChatCompletionService : IChatCompletionService
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
        var lastUser = chatHistory.LastOrDefault(m => m.Role == AuthorRole.User);
        var echoed = lastUser?.Content ?? string.Empty;
        IReadOnlyList<ChatMessageContent> result =
        [
            new ChatMessageContent(AuthorRole.Assistant, $"echo:{echoed}"),
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

file interface IDurableChatWorkflow
{
    Task<string> RunAsync(string userMessage);
}

file sealed class DurableChatWorkflow : IDurableChatWorkflow
{
    private readonly IDurableChatCompletionService _chat;

    public DurableChatWorkflow(IDurableChatCompletionService chat) => _chat = chat;

    [Workflow]
    public async Task<string> RunAsync(string userMessage)
    {
        var history = new List<DurableChatMessage> { new("user", userMessage) };
        var response = await _chat.CompleteAsync(history).ConfigureAwait(false);
        return response.Content ?? string.Empty;
    }
}
#pragma warning restore CA1812

file static class DurableChatScenarios
{
    public static async Task LlmCallIsCheckpointed(DbosBuilder builder)
    {
        var fakeChat = new CountingChatCompletionService();
        var kernelWithService = BuildKernelWithService(fakeChat);

        await using var dbos = builder
            .WithOptions(o => o with { ExecutorId = "dbos-durable-chat-test" })
            .Build();

        var chat = kernelWithService.AddDurableChatCompletion(dbos);
        dbos.RegisterProxy<IDurableChatWorkflow>(new DurableChatWorkflow(chat));

        await dbos.LaunchAsync();

        var handle = await dbos.StartWorkflowAsync<string>(
            workflowName: nameof(DurableChatWorkflow.RunAsync),
            className: typeof(DurableChatWorkflow).FullName,
            instanceName: null,
            args: ["hello"]);

        var result = await handle.GetResultAsync();
        Assert.Equal("echo:hello", result);
        Assert.Equal(1, fakeChat.InvocationCount);

        // The LLM call is recorded as a single step keyed on the wrapper method name.
        var steps = await dbos.ListWorkflowStepsAsync(handle.WorkflowId);
        Assert.Single(steps);
        Assert.Equal(0, steps[0].FunctionId);
        Assert.Equal(nameof(IDurableChatCompletionService.CompleteAsync), steps[0].FunctionName);
        Assert.NotNull(steps[0].Output);
    }

    public static async Task ReplayWithSameWorkflowIdSkipsLlmCall(DbosBuilder builder)
    {
        var fakeChat = new CountingChatCompletionService();
        var kernelWithService = BuildKernelWithService(fakeChat);

        await using var dbos = builder
            .WithOptions(o => o with { ExecutorId = "dbos-durable-chat-replay-test" })
            .Build();

        var chat = kernelWithService.AddDurableChatCompletion(dbos);
        dbos.RegisterProxy<IDurableChatWorkflow>(new DurableChatWorkflow(chat));

        await dbos.LaunchAsync();

        var workflowId = $"replay-{Guid.NewGuid()}";
        var options = new StartWorkflowOptions(workflowId: workflowId);

        var handle1 = await dbos.StartWorkflowAsync<string>(
            workflowName: nameof(DurableChatWorkflow.RunAsync),
            className: typeof(DurableChatWorkflow).FullName,
            instanceName: null,
            args: ["world"],
            options: options);
        var result1 = await handle1.GetResultAsync();

        // Re-issue with the same workflow ID — DBOS recognizes the existing workflow
        // and returns the cached completion without re-invoking the underlying LLM.
        var handle2 = await dbos.StartWorkflowAsync<string>(
            workflowName: nameof(DurableChatWorkflow.RunAsync),
            className: typeof(DurableChatWorkflow).FullName,
            instanceName: null,
            args: ["world"],
            options: options);
        var result2 = await handle2.GetResultAsync();

        Assert.Equal(result1, result2);
        Assert.Equal("echo:world", result2);
        Assert.Equal(1, fakeChat.InvocationCount);
    }

    private static Kernel BuildKernelWithService(IChatCompletionService service)
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(service);
        return builder.Build();
    }
}

[Collection("Postgres")]
public sealed class PostgresDurableChatCompletionServiceTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string Schema = "dbos_durable_chat_pg_test";
    private readonly PostgresFixture _fixture;

    public PostgresDurableChatCompletionServiceTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Postgres, Schema);
        await mgr.RunAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private DbosBuilder NewBuilder() =>
        Dbos.Builder("dbos-durable-chat-pg-test")
            .WithOptions(o => o with { Migrate = false })
            .UsePostgres(_fixture.ConnectionString, Schema);

    [Fact] public Task LlmCallIsCheckpointed() => DurableChatScenarios.LlmCallIsCheckpointed(NewBuilder());
    [Fact] public Task ReplayWithSameWorkflowIdSkipsLlmCall() => DurableChatScenarios.ReplayWithSameWorkflowIdSkipsLlmCall(NewBuilder());
}

public sealed class SqliteDurableChatCompletionServiceTests : IAsyncLifetime, IDisposable
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
        Dbos.Builder("dbos-durable-chat-sqlite-test")
            .WithOptions(o => o with { Migrate = false })
            .UseSqlite(_fixture.ConnectionString);

    [Fact] public Task LlmCallIsCheckpointed() => DurableChatScenarios.LlmCallIsCheckpointed(NewBuilder());
    [Fact] public Task ReplayWithSameWorkflowIdSkipsLlmCall() => DurableChatScenarios.ReplayWithSameWorkflowIdSkipsLlmCall(NewBuilder());
}
