namespace Dbos.Transact.Database;

/// <summary>
/// Encapsulates every SQL primitive that differs between Postgres and SQLite.
/// Implementations live in the dialect-specific projects
/// (<c>Dbos.Transact.Postgres</c>, <c>Dbos.Transact.Sqlite</c>).
/// A no-op stub (<see cref="NoOpSqlDialect"/>) is provided for unit tests.
/// </summary>
public interface ISqlDialect
{
    /// <summary>SQL type name for JSON columns: <c>jsonb</c> (Postgres) or <c>TEXT</c> (SQLite).</summary>
    string JsonColumnType { get; }

    /// <summary>SQL expression that returns the current UTC time as milliseconds since epoch.</summary>
    string EpochMillisExpression { get; }

    /// <summary>Whether this dialect supports Postgres-style schema namespacing.</summary>
    bool SupportsSchemas { get; }

    /// <summary>Whether this dialect supports <c>SELECT … FOR UPDATE SKIP LOCKED</c>.</summary>
    bool SupportsSkipLocked { get; }

    /// <summary>Whether this dialect supports <c>LISTEN</c>/<c>NOTIFY</c>.</summary>
    bool SupportsListenNotify { get; }

    /// <summary>
    /// Returns the schema prefix for use in SQL identifiers.
    /// Postgres: <c>"schema".</c> — SQLite: <c>""</c>.
    /// </summary>
    string SchemaPrefix(string schema);

    /// <summary>
    /// Split a SQL script into individual executable statements.
    /// Required for SQLite (which executes one statement per command);
    /// Postgres implementations may return the whole script as a single element.
    /// </summary>
    IReadOnlyList<string> SplitStatements(string sql);
}
