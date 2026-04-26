namespace Dbos.Transact.Json;

/// <summary>
/// Wire representation of workflow invocation arguments. Port of Java's
/// <c>JsonWorkflowArgs</c> record.
/// </summary>
public sealed record JsonWorkflowArgs(
    object?[]? PositionalArgs = null,
    IReadOnlyDictionary<string, object?>? NamedArgs = null)
{
    public static JsonWorkflowArgs FromPositional(params object?[] args) =>
        new(PositionalArgs: args);
}
