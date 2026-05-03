using System.Reflection;
using Dbos.Transact.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dbos.Transact.Hosting;

/// <summary>
/// <see cref="IServiceCollection"/> extensions that wire <see cref="Dbos"/> into the
/// generic .NET host. Mirrors the surface of the Java Spring Boot starter
/// (<c>@EnableConfigurationProperties(DBOSProperties.class)</c>, auto-config beans, lifecycle
/// management) but expressed as explicit DI registrations.
/// </summary>
public static class DbosHostingExtensions
{
    // Cached reflection handle for Dbos.RegisterProxy<T>(T, string?).
    private static readonly MethodInfo RegisterProxyOpenMethod =
        typeof(Dbos).GetMethod(nameof(Dbos.RegisterProxy), BindingFlags.Public | BindingFlags.Instance)
        ?? throw new InvalidOperationException($"Method {nameof(Dbos.RegisterProxy)} not found on {nameof(Dbos)}.");

    /// <summary>
    /// Registers <see cref="Dbos"/> as a singleton, configures the
    /// <see cref="DbosOptionsConfigurator"/> options chain, and registers
    /// <see cref="DbosHostedService"/> as an <see cref="IHostedService"/>.
    /// The <paramref name="configureBuilder"/> callback supplies the dialect (e.g.
    /// <c>builder.UseSqlite(connectionString)</c>) and any builder-level options.
    /// </summary>
    public static IServiceCollection AddDbos(
        this IServiceCollection services,
        Action<DbosBuilder> configureBuilder)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureBuilder);
        return AddDbosCore(services, defaultAppName: null, configureBuilder);
    }

    /// <summary>
    /// Convenience overload that supplies a default application name; equivalent to calling
    /// the base <see cref="AddDbos(IServiceCollection, Action{DbosBuilder})"/> after
    /// <c>services.Configure&lt;DbosOptionsConfigurator&gt;(o =&gt; o.Application.Name = appName)</c>,
    /// but with the appName treated as a *default* — any explicit configuration value still wins.
    /// </summary>
    public static IServiceCollection AddDbos(
        this IServiceCollection services,
        string appName,
        Action<DbosBuilder> configureBuilder)
    {
        ArgumentException.ThrowIfNullOrEmpty(appName);
        return AddDbosCore(services, defaultAppName: appName, configureBuilder);
    }

    private static IServiceCollection AddDbosCore(
        IServiceCollection services,
        string? defaultAppName,
        Action<DbosBuilder> configureBuilder)
    {
        services.AddOptions<DbosOptionsConfigurator>();

        services.TryAddSingleton(sp =>
        {
            var configurator = sp.GetRequiredService<IOptions<DbosOptionsConfigurator>>().Value;
            var options = configurator.BuildOptions(defaultAppName);
            var builder = Dbos.Builder(options);
            configureBuilder(builder);
            return builder.Build();
        });

        services.AddHostedService<DbosHostedService>();
        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="TImpl"/> as a singleton and binds <typeparamref name="TInterface"/>
    /// to a Dbos-built proxy. Resolving <typeparamref name="TInterface"/> calls
    /// <see cref="Dbos.RegisterProxy{T}(T, string?)"/> on the impl and returns the proxy.
    /// <see cref="DbosHostedService"/> resolves all registered interfaces before launch so the
    /// "register before launch" invariant holds even for callers that never resolve them themselves.
    /// </summary>
    public static IServiceCollection AddDbosWorkflow<TInterface, TImpl>(
        this IServiceCollection services,
        string? instanceName = null)
        where TInterface : class
        where TImpl : class, TInterface
    {
        ArgumentNullException.ThrowIfNull(services);
        AddDbosWorkflowCore(services, typeof(TInterface), typeof(TImpl), instanceName);
        return services;
    }

    /// <summary>
    /// Scans <paramref name="assembly"/> for concrete classes that declare methods annotated
    /// with <c>[Workflow]</c> or <c>[Step]</c> and auto-registers each (interface, impl) pair
    /// via <see cref="AddDbosWorkflow{TInterface,TImpl}"/>. Each concrete type must implement
    /// at least one interface that exposes the annotated method signatures; pairs where no
    /// matching interface can be found are silently skipped.
    /// </summary>
    /// <remarks>
    /// This is opt-in. Explicit <see cref="AddDbosWorkflow{TInterface,TImpl}"/> calls remain
    /// the primary registration path and are always supported.
    /// </remarks>
    public static IServiceCollection AddDbosWorkflowsFromAssembly(
        this IServiceCollection services,
        Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (var concreteType in assembly.GetExportedTypes()
            .Where(t => t.IsClass && !t.IsAbstract))
        {
            if (!HasWorkflowOrStepMethods(concreteType)) continue;
            foreach (var iface in FindWorkflowInterfaces(concreteType))
                AddDbosWorkflowCore(services, iface, concreteType, instanceName: null);
        }

        return services;
    }

    /// <summary>
    /// Registers a <see cref="Queue"/> with the <see cref="Dbos"/> instance before launch.
    /// </summary>
    public static IServiceCollection AddDbosQueue(this IServiceCollection services, Queue queue)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(queue);

        services.AddSingleton(new DbosQueueRegistration(queue));
        return services;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void AddDbosWorkflowCore(
        IServiceCollection services,
        Type interfaceType,
        Type implType,
        string? instanceName)
    {
        var closedRegisterProxy = RegisterProxyOpenMethod.MakeGenericMethod(interfaceType);

        services.TryAddSingleton(implType);

        services.AddSingleton(interfaceType, sp =>
        {
            var dbos = sp.GetRequiredService<Dbos>();
            var impl = sp.GetRequiredService(implType);
            return closedRegisterProxy.Invoke(dbos, [impl, instanceName])!;
        });

        services.AddSingleton(new DbosWorkflowRegistration(interfaceType, instanceName));
    }

    private static bool HasWorkflowOrStepMethods(Type type)
    {
        // Check declared methods on the concrete class first; then check its interfaces
        // because attributes may be placed on the interface method rather than the impl.
        if (HasAnnotatedMethod(type)) return true;
        foreach (var iface in type.GetInterfaces())
            if (HasAnnotatedMethod(iface)) return true;
        return false;

        static bool HasAnnotatedMethod(Type t) =>
            t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Any(m =>
                    m.IsDefined(typeof(WorkflowAttribute), inherit: false) ||
                    m.IsDefined(typeof(StepAttribute), inherit: false));
    }

    private static IEnumerable<Type> FindWorkflowInterfaces(Type concreteType)
    {
        // Names of methods annotated on the concrete class (attribute may live there instead of on the iface).
        var annotatedOnConcrete = concreteType
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m =>
                m.IsDefined(typeof(WorkflowAttribute), inherit: false) ||
                m.IsDefined(typeof(StepAttribute), inherit: false))
            .Select(m => m.Name)
            .ToHashSet(StringComparer.Ordinal);

        return concreteType.GetInterfaces()
            .Where(iface => iface.GetMethods().Any(m =>
                // Attribute on the interface method itself.
                m.IsDefined(typeof(WorkflowAttribute), inherit: false) ||
                m.IsDefined(typeof(StepAttribute), inherit: false) ||
                // Attribute on the corresponding concrete override.
                annotatedOnConcrete.Contains(m.Name)));
    }
}
