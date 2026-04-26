using System.Data;
using Dbos.Transact.Workflow;
using Dbos.Transact.Workflow.Internal;

namespace Dbos.Transact.Database.Daos;

/// <summary>
/// Data-access methods for the <c>workflow_steps</c> table.
/// Port of Java's <c>StepsDAO</c>. Dialect-specific SQL is provided by subclasses.
/// </summary>
public abstract class StepsDao
{
    /// <summary>
    /// Within an open transaction, checks whether a step was already recorded and returns
    /// its prior result, or records a new in-progress row.
    /// </summary>
    public abstract Task<StepResult> CheckStepExecutionTxnAsync(
        IDbConnection connection,
        string workflowId,
        int functionId,
        string functionName,
        CancellationToken ct = default);

    public abstract Task RecordStepResultTxnAsync(
        IDbConnection connection,
        StepResult result,
        long startTimeEpochMs,
        long endTimeEpochMs,
        CancellationToken ct = default);

    public abstract Task<IReadOnlyList<StepInfo>> ListWorkflowStepsAsync(
        string workflowId,
        bool loadOutput,
        int? limit,
        int? offset,
        CancellationToken ct = default);

    public abstract Task SleepAsync(string workflowId, int functionId, TimeSpan duration, CancellationToken ct = default);
}
