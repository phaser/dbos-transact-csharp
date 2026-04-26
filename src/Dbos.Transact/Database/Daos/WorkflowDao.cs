using System.Data;
using Dbos.Transact.Workflow;
using Dbos.Transact.Workflow.Internal;

namespace Dbos.Transact.Database.Daos;

/// <summary>
/// Data-access methods for the <c>workflow_status</c> table.
/// Port of Java's <c>WorkflowDAO</c>. Dialect-specific SQL is provided by subclasses.
/// </summary>
public abstract class WorkflowDao
{
    public abstract Task<WorkflowInitResult> InitWorkflowStatusAsync(
        WorkflowStatusInternal initStatus,
        int maxRetries,
        bool isRecoveryRequest,
        bool isDequeuedRequest,
        CancellationToken ct = default);

    public abstract Task RecordWorkflowOutputAsync(string workflowId, string? result, CancellationToken ct = default);

    public abstract Task RecordWorkflowErrorAsync(string workflowId, string? errorPayload, CancellationToken ct = default);

    public abstract Task<WorkflowStatus?> GetWorkflowStatusAsync(string workflowId, CancellationToken ct = default);

    public abstract Task<string?> GetWorkflowSerializationAsync(string workflowId, CancellationToken ct = default);

    public abstract Task<IReadOnlyList<WorkflowStatus>> ListWorkflowsAsync(ListWorkflowsInput input, CancellationToken ct = default);

    public abstract Task<IReadOnlyList<WorkflowAggregateRow>> GetWorkflowAggregatesAsync(GetWorkflowAggregatesInput input, CancellationToken ct = default);

    public abstract Task<IReadOnlyList<WorkflowStatus>> GetPendingWorkflowsAsync(IReadOnlyList<string> executorIds, string? appVersion, CancellationToken ct = default);

    public abstract Task RecordChildWorkflowAsync(
        IDbConnection connection,
        string workflowId,
        int functionId,
        string childWorkflowId,
        string? serialization,
        CancellationToken ct = default);

    public abstract Task<string?> CheckChildWorkflowAsync(string workflowId, int functionId, CancellationToken ct = default);

    public abstract Task<T> AwaitWorkflowResultAsync<T>(string workflowId, CancellationToken ct = default);

    public abstract Task CancelWorkflowsAsync(IReadOnlyList<string> workflowIds, CancellationToken ct = default);

    public abstract Task ResumeWorkflowsAsync(IReadOnlyList<string> workflowIds, string? queueName, CancellationToken ct = default);

    public abstract Task DeleteWorkflowsAsync(IReadOnlyList<string> workflowIds, bool deleteChildren, CancellationToken ct = default);

    public abstract Task<string> ForkWorkflowAsync(string originalWorkflowId, int startStep, ForkOptions options, CancellationToken ct = default);
}
