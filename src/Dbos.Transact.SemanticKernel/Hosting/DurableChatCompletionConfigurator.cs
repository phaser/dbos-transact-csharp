using Microsoft.SemanticKernel;

namespace Dbos.Transact.SemanticKernel.Hosting;

/// <summary>
/// Pre-launch hook that calls <see cref="DbosKernelExtensions.AddDurableChatCompletion"/>
/// against the live <see cref="Dbos"/> instance and stores the resulting proxy in the
/// shared <see cref="DurableChatCompletionHolder"/> so workflow impls that injected
/// <see cref="IDurableChatCompletionService"/> from DI receive it.
/// </summary>
internal sealed class DurableChatCompletionConfigurator : IDbosPreLaunchConfigurator
{
    private readonly Kernel _kernel;
    private readonly DurableChatCompletionHolder _holder;
    private readonly string? _instanceName;

    public DurableChatCompletionConfigurator(
        Kernel kernel,
        DurableChatCompletionHolder holder,
        string? instanceName)
    {
        _kernel = kernel;
        _holder = holder;
        _instanceName = instanceName;
    }

    public Task ConfigureAsync(Dbos dbos, CancellationToken cancellationToken = default)
    {
        _holder.Service = _kernel.AddDurableChatCompletion(dbos, _instanceName);
        return Task.CompletedTask;
    }
}
