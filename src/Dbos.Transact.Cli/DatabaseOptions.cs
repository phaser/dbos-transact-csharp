using System.CommandLine;
using System.Data.Common;
using Dbos.Transact.Database;
using Dbos.Transact.Postgres.Database;
using Dbos.Transact.Sqlite.Database;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace Dbos.Transact.Cli;

/// <summary>
/// Common database options shared across CLI subcommands. Mirrors Java's
/// <c>DatabaseOptions</c> mixin.
/// </summary>
internal static class DatabaseOptions
{
    public enum DialectKind { Postgres, Sqlite }

    public static readonly Option<string> DbUrl = new("--db-url", "-D")
    {
        Description =
            "Connection string for your DBOS system database (env: DBOS_SYSTEM_JDBC_URL).",
        DefaultValueFactory = _ =>
            Environment.GetEnvironmentVariable("DBOS_SYSTEM_JDBC_URL") ?? string.Empty,
    };

    public static readonly Option<string> Schema = new("--schema")
    {
        Description = "Database schema name (Postgres only).",
        DefaultValueFactory = _ => Constants.DbSchema,
    };

    public static readonly Option<DialectKind?> Dialect = new("--dialect")
    {
        Description =
            "Database dialect: postgres or sqlite. Autodetected from --db-url when omitted.",
    };

    /// <summary>Adds the shared database options to <paramref name="command"/>.</summary>
    public static void AddTo(Command command)
    {
        command.Options.Add(DbUrl);
        command.Options.Add(Schema);
        command.Options.Add(Dialect);
    }

    /// <summary>
    /// Resolves the dialect by preferring <paramref name="explicitDialect"/> when set, otherwise
    /// inspecting <paramref name="url"/> for SQLite-style markers.
    /// </summary>
    public static DialectKind ResolveDialect(DialectKind? explicitDialect, string url)
    {
        if (explicitDialect.HasValue) return explicitDialect.Value;

        var s = url?.Trim() ?? string.Empty;
        if (s.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
            || s.StartsWith("Filename=", StringComparison.OrdinalIgnoreCase)
            || s.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase)
            || s.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            return DialectKind.Sqlite;
        return DialectKind.Postgres;
    }

    /// <summary>Opens a fresh <see cref="DbConnection"/> for the resolved dialect.</summary>
    public static DbConnection OpenConnection(DialectKind dialect, string url) =>
        dialect switch
        {
            DialectKind.Postgres => new NpgsqlConnection(url),
            DialectKind.Sqlite => new SqliteConnection(url),
            _ => throw new InvalidOperationException($"Unsupported dialect: {dialect}"),
        };

    /// <summary>Builds a <see cref="SystemDatabase"/> for the resolved dialect.</summary>
    public static SystemDatabase CreateSystemDatabase(DialectKind dialect, string url, string schema) =>
        dialect switch
        {
            DialectKind.Postgres => new PostgresSystemDatabase(url, schema),
            DialectKind.Sqlite => new SqliteSystemDatabase(url),
            _ => throw new InvalidOperationException($"Unsupported dialect: {dialect}"),
        };
}
