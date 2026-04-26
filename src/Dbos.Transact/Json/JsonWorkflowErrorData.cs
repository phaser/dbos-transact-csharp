namespace Dbos.Transact.Json;

/// <summary>
/// Serializable data bag carried by a <see cref="PortableWorkflowException"/>.
/// Port of Java's <c>JsonWorkflowErrorData</c> record.
/// </summary>
public sealed record JsonWorkflowErrorData(
    string Name,
    string? Message,
    object? Code = null,
    object? Data = null);
