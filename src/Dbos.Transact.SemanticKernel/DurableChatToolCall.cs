namespace Dbos.Transact.SemanticKernel;

/// <summary>
/// A tool call requested by the LLM. The runner is responsible for dispatching it
/// (e.g. via <c>kernel.InvokeAsync(PluginName, FunctionName, args)</c>) and feeding the
/// result back as a <c>"tool"</c>-role <see cref="DurableChatMessage"/> in the next turn.
/// </summary>
/// <param name="Id">LLM-assigned identifier for this tool call. Used to correlate the
/// resulting tool message with the original call.</param>
/// <param name="PluginName">SK plugin name (e.g. the <c>pluginName</c> passed to
/// <see cref="DbosKernelExtensions.AddDbosPlugin{T}"/>).</param>
/// <param name="FunctionName">Tool method name as exposed to the kernel.</param>
/// <param name="Arguments">Stringified key→value arguments from the LLM. Stringification
/// keeps the dictionary JSON-friendly for DBOS checkpointing.</param>
public sealed record DurableChatToolCall(
    string Id,
    string PluginName,
    string FunctionName,
    IReadOnlyDictionary<string, string?> Arguments);
