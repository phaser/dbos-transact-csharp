using System.Data.Common;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Dbos.Transact.Migrations;

/// <summary>
/// Applies versioned SQL migrations to the DBOS system database.
/// Migrations are embedded resources under <c>Migrations/Sql/{Dialect}/</c>.
/// The version table tracks the highest applied migration index; re-running is idempotent.
/// </summary>
public sealed class MigrationManager
{
    public enum SqlDialect { Postgres, Sqlite }

    private static readonly Regex LeadingDigits = new(@"^(\d+)", RegexOptions.Compiled);

    private readonly DbConnection _connection;
    private readonly SqlDialect _dialect;
    private readonly string _schema;

    public MigrationManager(
        DbConnection connection,
        SqlDialect dialect,
        string schema = Constants.DbSchema)
    {
        _connection = connection;
        _dialect = dialect;
        _schema = schema;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        if (_connection.State != System.Data.ConnectionState.Open)
            await _connection.OpenAsync(ct);

        if (_dialect == SqlDialect.Postgres)
            await EnsureSchemaAsync(ct);

        await EnsureMigrationTableAsync(ct);

        var migrations = LoadMigrations();
        var current = await GetCurrentVersionAsync(ct);

        for (int i = current; i < migrations.Count; i++)
        {
            var version = i + 1;
            var raw = migrations[i];
            var sql = _dialect == SqlDialect.Postgres
                ? string.Format(CultureInfo.InvariantCulture, raw, _schema)
                : raw;

            await ExecuteMigrationAsync(sql, version, ct);
        }
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"CREATE SCHEMA IF NOT EXISTS \"{_schema}\"";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task EnsureMigrationTableAsync(CancellationToken ct)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = _dialect == SqlDialect.Postgres
            ? $"CREATE TABLE IF NOT EXISTS \"{_schema}\".dbos_migrations (version BIGINT NOT NULL PRIMARY KEY)"
            : "CREATE TABLE IF NOT EXISTS dbos_migrations (version INTEGER NOT NULL PRIMARY KEY)";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<int> GetCurrentVersionAsync(CancellationToken ct)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = _dialect == SqlDialect.Postgres
            ? $"SELECT version FROM \"{_schema}\".dbos_migrations ORDER BY version DESC LIMIT 1"
            : "SELECT version FROM dbos_migrations ORDER BY version DESC LIMIT 1";
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is null or DBNull ? 0 : Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private async Task ExecuteMigrationAsync(string sql, int version, CancellationToken ct)
    {
        var statements = _dialect == SqlDialect.Sqlite
            ? SplitStatements(sql)
            : [sql];

        foreach (var stmt in statements)
        {
            if (string.IsNullOrWhiteSpace(stmt)) continue;
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = stmt;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using var track = _connection.CreateCommand();
        track.CommandText = version == 1
            ? (_dialect == SqlDialect.Postgres
                ? $"INSERT INTO \"{_schema}\".dbos_migrations (version) VALUES ({version})"
                : $"INSERT INTO dbos_migrations (version) VALUES ({version})")
            : (_dialect == SqlDialect.Postgres
                ? $"UPDATE \"{_schema}\".dbos_migrations SET version = {version}"
                : $"UPDATE dbos_migrations SET version = {version}");
        await track.ExecuteNonQueryAsync(ct);
    }

    private List<string> LoadMigrations()
    {
        var assembly = typeof(MigrationManager).Assembly;
        var folder = _dialect == SqlDialect.Postgres ? "Postgres" : "Sqlite";
        var prefix = $"Dbos.Transact.Migrations.Sql.{folder}.";

        var resources = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix, StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.Ordinal))
            .OrderBy(n => ParseLeadingNumber(n[prefix.Length..]))
            .ToList();

        var scripts = new List<string>(resources.Count);
        foreach (var name in resources)
        {
            using var stream = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Embedded resource not found: {name}");
            using var reader = new StreamReader(stream);
            scripts.Add(reader.ReadToEnd());
        }
        return scripts;
    }

    private static int ParseLeadingNumber(string fileName)
    {
        var m = LeadingDigits.Match(fileName);
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : int.MaxValue;
    }

    internal static List<string> SplitStatements(string sql)
    {
        // Strip -- line comments before splitting so semicolons inside comments don't
        // produce spurious empty or invalid statements (e.g. "-- ...; no-op\nSELECT 1").
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
