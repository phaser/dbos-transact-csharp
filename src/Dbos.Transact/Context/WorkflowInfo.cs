namespace Dbos.Transact.Context;

/// <summary>
/// Identifies the parent workflow of a child workflow.
/// Port of Java <c>WorkflowInfo</c> record.
/// </summary>
/// <param name="WorkflowId">The unique ID of the parent workflow.</param>
/// <param name="FunctionId">The step number within the parent workflow that created the child.</param>
public sealed record WorkflowInfo(string WorkflowId, int FunctionId);
