using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.SemanticKernel;

namespace Dbos.Transact.SemanticKernel.Hosting;

/// <summary>
/// <see cref="IServiceCollection"/> extensions that register Semantic Kernel integrations
/// declaratively in DI. Pairs with <c>Dbos.Transact.Hosting</c>'s <c>AddDbos</c> and
/// <c>AddDbosWorkflow</c> calls — the actual wiring against the <see cref="Dbos"/> instance
/// happens inside <see cref="IDbosPreLaunchConfigurator"/> hooks resolved by
/// <c>DbosHostedService</c> at host start, before <see cref="Dbos.LaunchAsync"/>.
/// </summary>
public static class DbosSemanticKernelHostingExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TImpl"/> as a singleton (keyed on its concrete type)
    /// and a pre-launch configurator that adds <typeparamref name="TInterface"/>'s
    /// <c>[KernelFunction]+[Step]</c> methods as a Semantic Kernel plugin against the
    /// kernel resolved from DI.
    /// </summary>
    /// <typeparam name="TInterface">The tool interface (must declare
    /// <c>[KernelFunction]+[Step]</c> methods).</typeparam>
    /// <typeparam name="TImpl">A concrete implementation of <typeparamref name="TInterface"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="pluginName">Plugin name visible to the kernel/agent. Defaults to the
    /// interface name with a leading <c>I</c> stripped.</param>
    /// <param name="instanceName">Optional DBOS instance name.</param>
    public static IServiceCollection AddDbosSemanticKernelPlugin<TInterface, TImpl>(
        this IServiceCollection services,
        string? pluginName = null,
        string? instanceName = null)
        where TInterface : class
        where TImpl : class, TInterface
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<TImpl>();
        services.AddSingleton<IDbosPreLaunchConfigurator>(sp =>
        {
            var kernel = sp.GetRequiredService<Kernel>();
            var impl = sp.GetRequiredService<TImpl>();
            return new SemanticKernelPluginConfigurator<TInterface, TImpl>(
                kernel, impl, pluginName, instanceName);
        });
        return services;
    }

    /// <summary>
    /// Registers a pre-launch configurator that wraps the kernel's registered
    /// <c>IChatCompletionService</c> with <see cref="IDurableChatCompletionService"/> and
    /// exposes the resulting proxy via DI. Inject <see cref="IDurableChatCompletionService"/>
    /// into your workflow class to use it.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="instanceName">Optional DBOS instance name.</param>
    public static IServiceCollection AddDbosDurableChatCompletion(
        this IServiceCollection services,
        string? instanceName = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<DurableChatCompletionHolder>();
        services.AddSingleton<IDurableChatCompletionService>(sp =>
        {
            var holder = sp.GetRequiredService<DurableChatCompletionHolder>();
            return holder.Service
                ?? throw new InvalidOperationException(
                    "IDurableChatCompletionService is not yet wired. " +
                    "Resolve it after the host has started — DbosHostedService runs the " +
                    "configurator that populates it during StartAsync.");
        });
        services.AddSingleton<IDbosPreLaunchConfigurator>(sp =>
        {
            var kernel = sp.GetRequiredService<Kernel>();
            var holder = sp.GetRequiredService<DurableChatCompletionHolder>();
            return new DurableChatCompletionConfigurator(kernel, holder, instanceName);
        });
        return services;
    }
}
