using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Dbos.Transact.SemanticKernel;

/// <summary>
/// Concrete <see cref="IDurableChatCompletionService"/>: thin adapter over an
/// <c>IChatCompletionService</c> that converts DBOS-friendly DTOs to/from SK's
/// <c>ChatHistory</c> / <c>ChatMessageContent</c> graph. The <c>[Step]</c> attribute
/// lives on the interface, so DBOS's proxy interceptor checkpoints every call.
/// </summary>
public sealed class DurableChatCompletionService : IDurableChatCompletionService
{
    private readonly IChatCompletionService _inner;
    private readonly Kernel _kernel;

    public DurableChatCompletionService(IChatCompletionService inner, Kernel kernel)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(kernel);
        _inner = inner;
        _kernel = kernel;
    }

    public async Task<DurableChatResponse> CompleteAsync(
        IReadOnlyList<DurableChatMessage> history,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(history);

        var skHistory = ToSemanticKernelHistory(history);

        var settings = new PromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false),
        };

        var responses = await _inner.GetChatMessageContentsAsync(skHistory, settings, _kernel, ct).ConfigureAwait(false);
        if (responses.Count == 0)
        {
            return new DurableChatResponse(null, []);
        }
        var first = responses[0];

        var toolCalls = first.Items.OfType<FunctionCallContent>()
            .Select(FromFunctionCall)
            .ToList();

        return new DurableChatResponse(first.Content, toolCalls);
    }

    private static ChatHistory ToSemanticKernelHistory(IReadOnlyList<DurableChatMessage> history)
    {
        var skHistory = new ChatHistory();
        foreach (var msg in history)
        {
            var role = new AuthorRole(msg.Role);
            if (role == AuthorRole.Tool && msg.ToolCallId is not null)
            {
                // Tool-result messages must carry a FunctionResultContent so SK / the
                // LLM can correlate them with the originating tool call.
                var items = new ChatMessageContentItemCollection
                {
                    new FunctionResultContent(callId: msg.ToolCallId, result: msg.Content),
                };
                skHistory.Add(new ChatMessageContent(role, items));
            }
            else
            {
                skHistory.Add(new ChatMessageContent(role, msg.Content));
            }
        }
        return skHistory;
    }

    private static DurableChatToolCall FromFunctionCall(FunctionCallContent fc)
    {
        var args = fc.Arguments is null
            ? new Dictionary<string, string?>()
            : fc.Arguments.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString());
        return new DurableChatToolCall(
            Id: fc.Id ?? string.Empty,
            PluginName: fc.PluginName ?? string.Empty,
            FunctionName: fc.FunctionName ?? string.Empty,
            Arguments: args);
    }
}
