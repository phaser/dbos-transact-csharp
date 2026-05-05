namespace Dbos.Transact.SemanticKernel;

/// <summary>
/// One message in a durable chat history. JSON-friendly counterpart to Semantic Kernel's
/// <c>ChatMessageContent</c> — kept minimal so DBOS can checkpoint a history without
/// needing to round-trip SK's polymorphic content-item collection through a JSON serializer.
/// </summary>
/// <param name="Role">One of <c>"system"</c>, <c>"user"</c>, <c>"assistant"</c>, or <c>"tool"</c>.</param>
/// <param name="Content">Text content of the message. For tool messages, this is the
/// stringified result returned to the LLM.</param>
/// <param name="ToolCallId">For <c>"tool"</c> messages, the ID of the tool call this is
/// answering. Null for other roles.</param>
public sealed record DurableChatMessage(string Role, string Content, string? ToolCallId = null);
