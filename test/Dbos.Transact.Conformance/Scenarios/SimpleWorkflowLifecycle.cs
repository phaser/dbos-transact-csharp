using System.Reflection;
using Dbos.Transact.Execution;
using Dbos.Transact.Internal;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Conformance.Scenarios;

/// <summary>
/// Scenario: a single workflow with one step runs to completion (SUCCESS).
/// Verifies the minimal observable lifecycle: the workflow row transitions to SUCCESS
/// and exactly one step row is written.
/// Mirrors the Java BearService / HawkService "simple workflow" patterns.
/// </summary>
public sealed class SimpleWorkflowLifecycle : ConformanceScenario
{
    public override string Name => "simple_workflow_lifecycle";

    public override ScenarioExpectation Expected => new(
        WorkflowStatus: "SUCCESS",
        StepCount: 1);

    public override async Task<string> RunAsync(
        DbosExecutor executor,
        CancellationToken cancellationToken = default)
    {
        var step = new IncrementStep();
        var proxy = DbosProxyFactory.CreateProxy<IIncrementStep>(
            step, instanceName: null, executor.CreateInvocationHandler());

        var workflowTarget = new IncrementWorkflow(proxy);
        var rw = new RegisteredWorkflow(
            WorkflowName: nameof(IncrementWorkflow.RunAsync),
            ClassName: nameof(IncrementWorkflow),
            InstanceName: null,
            Target: workflowTarget,
            WorkflowMethod: typeof(IncrementWorkflow)
                .GetMethod(nameof(IncrementWorkflow.RunAsync))!,
            MaxRecoveryAttempts: 3,
            SerializationStrategy: SerializationStrategy.Default);

        var handle = await executor.StartWorkflowAsync<int>(rw, args: [41], ct: cancellationToken);
        await handle.GetResultAsync(cancellationToken);
        return handle.WorkflowId;
    }
}

// ── Workflow and step types used by this scenario ─────────────────────────────

file interface IIncrementStep
{
    [Step]
    Task<int> IncrementAsync(int value);
}

file sealed class IncrementStep : IIncrementStep
{
    public Task<int> IncrementAsync(int value) => Task.FromResult(value + 1);
}

file sealed class IncrementWorkflow(IIncrementStep steps)
{
    [Workflow]
    public Task<int> RunAsync(int n) => steps.IncrementAsync(n);
}
