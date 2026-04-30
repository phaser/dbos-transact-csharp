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

        services.TryAddSingleton<TImpl>();

        services.AddSingleton<TInterface>(sp =>
        {
            var dbos = sp.GetRequiredService<Dbos>();
            var impl = sp.GetRequiredService<TImpl>();
            return dbos.RegisterProxy<TInterface>(impl, instanceName);
        });

        services.AddSingleton(new DbosWorkflowRegistration(typeof(TInterface), instanceName));
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
}
