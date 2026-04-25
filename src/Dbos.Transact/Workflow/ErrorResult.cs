namespace Dbos.Transact.Workflow;

/// <summary>
/// Serialized representation of an error produced by a workflow or step.
/// Deserialization logic is implemented in a later wave alongside IDbosSerializer.
/// </summary>
public sealed record ErrorResult(
    string? ClassName,
    string? Message,
    string? SerializedError,
    string? Serialization,
    Exception? Exception);
