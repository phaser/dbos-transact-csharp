using Dbos.Transact.Database;

namespace Dbos.Transact.Postgres.Database;

/// <summary>
/// <see cref="ISqlDialect"/> implementation for PostgreSQL.
/// </summary>
public sealed class PostgresSqlDialect : ISqlDialect
{
    public static readonly PostgresSqlDialect Instance = new();

    public string JsonColumnType => "TEXT";

    /// <summary>Current epoch in milliseconds — standard Postgres expression.</summary>
    public string EpochMillisExpression =>
        "(EXTRACT(epoch FROM now()) * 1000::numeric)::bigint";

    public bool SupportsSchemas => true;
    public bool SupportsSkipLocked => true;
    public bool SupportsListenNotify => true;

    public string SchemaPrefix(string schema) =>
        string.IsNullOrEmpty(schema) ? string.Empty : $"\"{schema}\".";

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
