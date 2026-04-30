namespace Dbos.Transact.Hosting;

/// <summary>
/// Marker registered in DI by <c>AddDbosWorkflow</c> so <see cref="DbosHostedService"/>
/// can enumerate the workflow interfaces to resolve (and thereby register their proxies)
/// before <see cref="Dbos.LaunchAsync"/>.
/// </summary>
public sealed class DbosWorkflowRegistration
{
    public Type InterfaceType { get; }
    public string? InstanceName { get; }

    public DbosWorkflowRegistration(Type interfaceType, string? instanceName = null)
    {
        ArgumentNullException.ThrowIfNull(interfaceType);
        InterfaceType = interfaceType;
        InstanceName = instanceName;
    }
}
