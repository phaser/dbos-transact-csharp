using Dbos.Transact.Execution;

namespace Dbos.Transact.Conformance;

/// <summary>
/// Base class for a conformance scenario — a self-contained workflow + its expected observable effects.
/// Port fidelity: mirrors the scenario structure used in the Java test suite (BearService, HawkService).
/// </summary>
public abstract class ConformanceScenario
{
    /// <summary>Short identifier used in fixture file names and test output.</summary>
    public abstract string Name { get; }

    /// <summary>
    /// Register workflow types and start the workflow under test.
    /// Returns the workflow ID of the completed workflow (after awaiting its result).
    /// </summary>
    public abstract Task<string> RunAsync(
        DbosExecutor executor,
        CancellationToken cancellationToken = default);

    /// <summary>Declarative expected DB state once the workflow has completed.</summary>
    public abstract ScenarioExpectation Expected { get; }
}

/// <summary>
/// Declarative expectation for a completed workflow's observable DB state.
/// Values that are <c>null</c> are not asserted.
/// </summary>
public sealed record ScenarioExpectation(
    string WorkflowStatus,
    int StepCount,
    string? OutputJson = null,
    string? ErrorTypeName = null
);
