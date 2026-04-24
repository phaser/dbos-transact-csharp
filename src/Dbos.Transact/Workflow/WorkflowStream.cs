namespace Dbos.Transact.Workflow;

#pragma warning disable CA1711 // name mirrors the Java API; renaming would break port fidelity
public sealed record WorkflowStream(
    string? Key,
    string? Value,
    int Offset,
    int StepId,
    string? Serialization);
#pragma warning restore CA1711
