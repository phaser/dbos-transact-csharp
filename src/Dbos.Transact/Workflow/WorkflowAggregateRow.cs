namespace Dbos.Transact.Workflow;

public sealed record WorkflowAggregateRow(IReadOnlyDictionary<string, string?> Group, long Count);
