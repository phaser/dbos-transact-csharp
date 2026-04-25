namespace Dbos.Transact.Exceptions;

/// <summary>
/// Thrown when the system database cannot be reached despite numerous retries.
/// </summary>
public sealed class DbosSystemDatabaseException : DbosException
{
    /// <summary>A recent exception received from the system database connection.</summary>
    public Exception DatabaseException { get; }

    public DbosSystemDatabaseException(Exception e)
        : base($"System database access error: {e.Message}", e)
    {
        DatabaseException = e;
    }
}
