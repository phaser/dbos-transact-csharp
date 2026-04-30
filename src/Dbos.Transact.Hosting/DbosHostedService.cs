using Microsoft.Extensions.Hosting;

namespace Dbos.Transact.Hosting;

/// <summary>
/// <see cref="IHostedService"/> that drives the <see cref="Dbos"/> lifecycle from the .NET host:
/// resolves any <c>AddDbosWorkflow</c> registrations to trigger proxy registration, then calls
/// <see cref="Dbos.LaunchAsync"/> on host start and <see cref="Dbos.ShutdownAsync"/> on host stop.
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
    private readonly IServiceProvider _serviceProvider;

    public DbosHostedService(
        Dbos dbos,
        IEnumerable<DbosWorkflowRegistration> workflowRegistrations,
        IEnumerable<DbosQueueRegistration> queueRegistrations,
        IServiceProvider serviceProvider)
    {
        _dbos = dbos;
        _workflowRegistrations = workflowRegistrations;
        _queueRegistrations = queueRegistrations;
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Apply AddDbosQueue registrations before launch.
        foreach (var queueReg in _queueRegistrations)
            _dbos.RegisterQueue(queueReg.Queue);

        // Resolving each registered workflow interface triggers the factory in
        // DbosHostingExtensions.AddDbosWorkflow, which calls Dbos.RegisterProxy on the impl.
        // This must happen before LaunchAsync, since RegisterProxy throws after launch.
        foreach (var reg in _workflowRegistrations)
            _ = _serviceProvider.GetService(reg.InterfaceType);

        await _dbos.LaunchAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => _dbos.ShutdownAsync();
}
