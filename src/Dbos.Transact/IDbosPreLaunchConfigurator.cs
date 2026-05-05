namespace Dbos.Transact;

/// <summary>
/// Pre-launch configuration hook. <see cref="Dbos.Transact.Hosting.DbosHostedService"/>
/// resolves all <see cref="IDbosPreLaunchConfigurator"/> instances from DI and invokes
/// each one's <see cref="ConfigureAsync"/> before <see cref="Dbos.LaunchAsync"/>.
///
/// Integration packages (e.g. <c>Dbos.Transact.SemanticKernel</c>) implement this to
/// register plugins, proxies, or other Dbos-instance-scoped state that must exist
/// before launch. The contract is "RegisterProxy / RegisterQueue must be safe to call
/// inside ConfigureAsync" — anything that's allowed pre-launch is allowed here.
/// </summary>
public interface IDbosPreLaunchConfigurator
{
    /// <summary>
    /// Performs pre-launch configuration against <paramref name="dbos"/>. Called once
    /// per host start, after the <see cref="Dbos"/> instance is built but before
    /// <see cref="Dbos.LaunchAsync"/>.
    /// </summary>
    Task ConfigureAsync(Dbos dbos, CancellationToken cancellationToken = default);
}
