namespace Dbos.Transact.SemanticKernel;

/// <summary>
/// One LLM turn's result. Either a final <see cref="Content"/> string (no tool calls,
/// agent loop should terminate), or one or more <see cref="ToolCalls"/> the runner
/// must dispatch before the next turn.
/// </summary>
/// <param name="Content">Final text from the LLM, if any.</param>
/// <param name="ToolCalls">Tool calls the LLM is requesting. Empty when the message is
/// a final answer.</param>
public sealed record DurableChatResponse(
    string? Content,
    IReadOnlyList<DurableChatToolCall> ToolCalls);
