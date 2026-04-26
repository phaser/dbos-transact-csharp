namespace Dbos.Transact.Database;

/// <summary>
/// In-memory no-op <see cref="ISqlDialect"/> for unit tests that exercise higher-level code
/// without requiring a real database connection.
/// </summary>
internal sealed class NoOpSqlDialect : ISqlDialect
{
    public static readonly NoOpSqlDialect Instance = new();

    public string JsonColumnType => "TEXT";
    public string EpochMillisExpression => "0";
    public bool SupportsSchemas => false;
    public bool SupportsSkipLocked => false;
    public bool SupportsListenNotify => false;

    public string SchemaPrefix(string schema) => string.Empty;

    public IReadOnlyList<string> SplitStatements(string sql) =>
        sql.Split(';')
           .Select(s => s.Trim())
           .Where(s => s.Length > 0)
           .ToList();
}
