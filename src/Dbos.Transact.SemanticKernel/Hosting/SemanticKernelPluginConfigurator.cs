using Microsoft.SemanticKernel;

namespace Dbos.Transact.SemanticKernel.Hosting;

/// <summary>
/// Pre-launch hook that calls <see cref="DbosKernelExtensions.AddDbosPlugin{T}"/> against
/// the live <see cref="Dbos"/> instance. Registered by
/// <see cref="DbosSemanticKernelHostingExtensions.AddDbosSemanticKernelPlugin{TInterface, TImpl}"/>.
/// </summary>
internal sealed class SemanticKernelPluginConfigurator<TInterface, TImpl> : IDbosPreLaunchConfigurator
    where TInterface : class
    where TImpl : class, TInterface
{
    private readonly Kernel _kernel;
    private readonly TImpl _impl;
    private readonly string? _pluginName;
    private readonly string? _instanceName;

    public SemanticKernelPluginConfigurator(
        Kernel kernel,
        TImpl impl,
        string? pluginName,
        string? instanceName)
    {
        _kernel = kernel;
        _impl = impl;
        _pluginName = pluginName;
        _instanceName = instanceName;
    }

    public Task ConfigureAsync(Dbos dbos, CancellationToken cancellationToken = default)
    {
        _kernel.AddDbosPlugin<TInterface>(dbos, _impl, _pluginName, _instanceName);
        return Task.CompletedTask;
    }
}
