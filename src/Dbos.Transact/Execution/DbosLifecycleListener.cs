namespace Dbos.Transact.Execution;

/// <summary>
/// Callback interface for events around the DBOS executor lifecycle.
/// Port of Java's <c>DBOSLifecycleListener</c>.
/// Implementations are registered before launch and receive notifications
/// when the executor starts accepting and stops processing workflows.
/// </summary>
public interface IDbosLifecycleListener
{
    /// <summary>Called after the executor is launched and ready to run workflows.</summary>
    void DbosLaunched();

    /// <summary>Called before the executor stops processing workflows during shutdown.</summary>
    void DbosShutDown();
}
