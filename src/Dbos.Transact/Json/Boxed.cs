namespace Dbos.Transact.Json;

/// <summary>
/// Generic wrapper that lets the serializer distinguish between a null return value and
/// "no value recorded yet". Used when a workflow or step returns <c>null</c> but the
/// result row still needs to carry type information.
/// </summary>
public sealed record Boxed<T>(T? Value);

/// <summary>Non-generic factory helpers for <see cref="Boxed{T}"/>.</summary>
public static class Boxed
{
    public static Boxed<T> Of<T>(T? value) => new(value);
}
