namespace Dbos.Transact.Workflow;

public sealed record WorkflowEvent(
    string? Key,
    string? Value,
    string? Serialization);
