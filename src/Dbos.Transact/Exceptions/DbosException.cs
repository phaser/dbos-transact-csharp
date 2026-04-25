namespace Dbos.Transact.Exceptions;

/// <summary>
/// Base class for all DBOS-specific exceptions.
/// </summary>
public abstract class DbosException : Exception
{
    protected DbosException(string message) : base(message) { }
    protected DbosException(string message, Exception? innerException) : base(message, innerException) { }
}
