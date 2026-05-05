using Dbos.Transact.Workflow;

namespace Dbos.Transact.SemanticKernel;

/// <summary>
/// Durable wrapper around Semantic Kernel's <c>IChatCompletionService</c>. Each call
/// is checkpointed by DBOS as a single <see cref="StepAttribute"/>, so on workflow
/// recovery the cached response is returned without re-issuing the LLM call (no token
/// re-spend, no decision drift).
/// </summary>
/// <remarks>
/// Tool calls are returned as data, not auto-executed: the impl uses
/// <c>FunctionChoiceBehavior.Auto(autoInvoke: false)</c>. The workflow body is
/// responsible for dispatching each tool call (typically via
/// <c>kernel.InvokeAsync(...)</c> against a plugin registered with
/// <see cref="DbosKernelExtensions.AddDbosPlugin{T}"/>) and feeding the result back
/// in the next call. This keeps both LLM turns and tool calls as top-level steps —
/// no nested-step semantics required.
/// </remarks>
public interface IDurableChatCompletionService
{
    /// <summary>
    /// Issues one LLM turn against the supplied history. Checkpointed as a single step.
    /// </summary>
    [Step]
    Task<DurableChatResponse> CompleteAsync(
        IReadOnlyList<DurableChatMessage> history,
        CancellationToken ct = default);
}
