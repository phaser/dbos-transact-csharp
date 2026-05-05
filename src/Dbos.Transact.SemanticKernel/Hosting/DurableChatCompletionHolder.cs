namespace Dbos.Transact.SemanticKernel.Hosting;

/// <summary>
/// DI-injectable holder for the late-bound <see cref="IDurableChatCompletionService"/>.
/// The service is a DBOS proxy that doesn't exist until the pre-launch configurator runs;
/// the holder lets workflow classes inject <see cref="IDurableChatCompletionService"/> at
/// DI-build time and read the populated instance once configurators have run.
/// </summary>
internal sealed class DurableChatCompletionHolder
{
    public IDurableChatCompletionService? Service { get; set; }
}
