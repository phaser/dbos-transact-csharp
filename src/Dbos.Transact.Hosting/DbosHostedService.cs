using Microsoft.Extensions.Hosting;

namespace Dbos.Transact.Hosting;

/// <summary>
/// <see cref="IHostedService"/> that drives the <see cref="Dbos"/> lifecycle from the .NET host:
/// applies queue registrations, runs <see cref="IDbosPreLaunchConfigurator"/> hooks (so
/// integration packages like <c>Dbos.Transact.SemanticKernel</c> can wire plugins / proxies
/// against the live <see cref="Dbos"/> instance), resolves <c>AddDbosWorkflow</c> registrations
/// to trigger proxy registration, then calls <see cref="Dbos.LaunchAsync"/> on host start and
/// <see cref="Dbos.ShutdownAsync"/> on host stop.
///
/// Ordering parity with Java's <c>DBOSAutoConfiguration.DBOSLifecycle</c>: workflows must be
/// registered before launch; the hosted service triggers registration eagerly in
/// <see cref="StartAsync"/> by resolving each registered workflow interface.
/// </summary>
public sealed class DbosHostedService : IHostedService
{
    private readonly Dbos _dbos;
    private readonly IEnumerable<DbosWorkflowRegistration> _workflowRegistrations;
    private readonly IEnumerable<DbosQueueRegistration> _queueRegistrations;
    private readonly IEnumerable<IDbosPreLaunchConfigurator> _preLaunchConfigurators;
    private readonly IServiceProvider _serviceProvider;

    public DbosHostedService(
        Dbos dbos,
        IEnumerable<DbosWorkflowRegistration> workflowRegistrations,
        IEnumerable<DbosQueueRegistration> queueRegistrations,
        IEnumerable<IDbosPreLaunchConfigurator> preLaunchConfigurators,
        IServiceProvider serviceProvider)
    {
        _dbos = dbos;
        _workflowRegistrations = workflowRegistrations;
        _queueRegistrations = queueRegistrations;
        _preLaunchConfigurators = preLaunchConfigurators;
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Apply AddDbosQueue registrations before launch.
        foreach (var queueReg in _queueRegistrations)
            _dbos.RegisterQueue(queueReg.Queue);

        // Run integration-package pre-launch hooks before workflow registration so that
        // workflow impls can inject services those configurators populate (e.g. the
        // late-bound IDurableChatCompletionService proxy from Dbos.Transact.SemanticKernel).
        foreach (var configurator in _preLaunchConfigurators)
            await configurator.ConfigureAsync(_dbos, cancellationToken).ConfigureAwait(false);

        // Resolving each registered workflow interface triggers the factory in
        // DbosHostingExtensions.AddDbosWorkflow, which calls Dbos.RegisterProxy on the impl.
        // This must happen before LaunchAsync, since RegisterProxy throws after launch.
        foreach (var reg in _workflowRegistrations)
            _ = _serviceProvider.GetService(reg.InterfaceType);

        await _dbos.LaunchAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => _dbos.ShutdownAsync();
}
