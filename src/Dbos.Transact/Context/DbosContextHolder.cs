namespace Dbos.Transact.Context;

/// <summary>
/// Ambient context holder backed by <see cref="AsyncLocal{T}"/>.
/// Port of Java <c>DBOSContextHolder</c> (Java uses <c>ThreadLocal</c>; C# uses
/// <c>AsyncLocal</c> so that child tasks inherit a snapshot of the current context
/// without propagating mutations back to the parent).
/// </summary>
public static class DbosContextHolder
{
    private static readonly AsyncLocal<DbosContext?> _holder = new();

    /// <summary>
    /// Returns the current context, or <c>null</c> if none has been set for this async flow.
    /// </summary>
    public static DbosContext? Get() => _holder.Value;

    /// <summary>Sets the context for the current async flow.</summary>
    public static void Set(DbosContext context) => _holder.Value = context;

    /// <summary>Clears the context for the current async flow.</summary>
    public static void Clear() => _holder.Value = null;
}
