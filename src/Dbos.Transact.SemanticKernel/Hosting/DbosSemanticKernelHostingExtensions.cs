using System.Reflection;
using System.Runtime.CompilerServices;
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
        AddDbosSemanticKernelPluginCore(services, typeof(TInterface), typeof(TImpl), pluginName, instanceName);
        return services;
    }

    /// <summary>
    /// Scans <paramref name="assembly"/> for concrete types whose interfaces declare
    /// <c>[KernelFunction]</c>-decorated methods and auto-registers each
    /// <c>(interface, impl)</c> pair via
    /// <see cref="AddDbosSemanticKernelPlugin{TInterface, TImpl}"/>. Each plugin gets the
    /// default name (interface name with a leading <c>I</c> stripped); use the explicit
    /// <see cref="AddDbosSemanticKernelPlugin{TInterface, TImpl}"/> overload when you need
    /// a custom <c>pluginName</c> or <c>instanceName</c>.
    /// </summary>
    public static IServiceCollection AddDbosSemanticKernelPluginsFromAssembly(
        this IServiceCollection services,
        Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (var concreteType in assembly.GetExportedTypes()
            .Where(t => t.IsClass && !t.IsAbstract))
        {
            foreach (var iface in FindKernelPluginInterfaces(concreteType))
                AddDbosSemanticKernelPluginCore(services, iface, concreteType, pluginName: null, instanceName: null);
        }

        return services;
    }

    /// <summary>
    /// Convenience overload of
    /// <see cref="AddDbosSemanticKernelPluginsFromAssembly(IServiceCollection, Assembly)"/>
    /// that infers the caller's assembly via <see cref="Assembly.GetCallingAssembly"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddDbosSemanticKernelPluginsFromAssembly(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return AddDbosSemanticKernelPluginsFromAssembly(services, Assembly.GetCallingAssembly());
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

    // ── Private helpers ───────────────────────────────────────────────────────

    private static readonly Type GenericConfiguratorOpenType =
        typeof(SemanticKernelPluginConfigurator<,>);

    private static void AddDbosSemanticKernelPluginCore(
        IServiceCollection services,
        Type interfaceType,
        Type implType,
        string? pluginName,
        string? instanceName)
    {
        services.TryAddSingleton(implType);

        // Build a SemanticKernelPluginConfigurator<TInterface, TImpl> via reflection so the
        // generic registration helper above and the assembly-scan path can share one core.
        var closedConfiguratorType = GenericConfiguratorOpenType.MakeGenericType(interfaceType, implType);
        var ctor = closedConfiguratorType.GetConstructors()[0];

        services.AddSingleton(typeof(IDbosPreLaunchConfigurator), sp =>
        {
            var kernel = sp.GetRequiredService<Kernel>();
            var impl = sp.GetRequiredService(implType);
            return ctor.Invoke([kernel, impl, pluginName, instanceName]);
        });
    }

    // Soft-coupled detection — string-match the attribute type so the assembly-scan path
    // never has to reference Microsoft.SemanticKernel attribute types directly.
    private const string KernelFunctionAttributeFullName = "Microsoft.SemanticKernel.KernelFunctionAttribute";

    private static IEnumerable<Type> FindKernelPluginInterfaces(Type concreteType) =>
        concreteType.GetInterfaces()
            .Where(iface => iface.GetMethods().Any(m =>
                m.GetCustomAttributesData().Any(a =>
                    string.Equals(a.AttributeType.FullName, KernelFunctionAttributeFullName, StringComparison.Ordinal))));
}
