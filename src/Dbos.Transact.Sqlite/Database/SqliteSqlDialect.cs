using Dbos.Transact.Database;

namespace Dbos.Transact.Sqlite.Database;

/// <summary>
/// <see cref="ISqlDialect"/> implementation for SQLite.
/// </summary>
public sealed class SqliteSqlDialect : ISqlDialect
{
    public static readonly SqliteSqlDialect Instance = new();

    public string JsonColumnType => "TEXT";

    /// <summary>Current epoch in milliseconds — SQLite expression using strftime.</summary>
    public string EpochMillisExpression =>
        "(CAST(strftime('%s', 'now') AS INTEGER) * 1000)";

    public bool SupportsSchemas => false;
    public bool SupportsSkipLocked => false;
    public bool SupportsListenNotify => false;

    public string SchemaPrefix(string schema) => string.Empty;

    public IReadOnlyList<string> SplitStatements(string sql)
    {
        var withoutComments = string.Join('\n', sql.Split('\n').Select(line =>
        {
            var idx = line.IndexOf("--", StringComparison.Ordinal);
            return idx >= 0 ? line[..idx] : line;
        }));
        return withoutComments.Split(';')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }
}
