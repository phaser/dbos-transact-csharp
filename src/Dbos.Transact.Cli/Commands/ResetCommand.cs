using System.CommandLine;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace Dbos.Transact.Cli.Commands;

internal static class ResetCommand
{
    private static readonly Option<bool> Yes = new("--yes", "-y")
    {
        Description = "Skip the confirmation prompt.",
    };

    public static Command Build()
    {
        var cmd = new Command("reset", "Reset the DBOS system database (destructive).");
        DatabaseOptions.AddTo(cmd);
        cmd.Options.Add(Yes);

        cmd.SetAction(async (parseResult, ct) =>
        {
            var stdout = parseResult.InvocationConfiguration.Output;
            var stderr = parseResult.InvocationConfiguration.Error;

            var url = parseResult.GetValue(DatabaseOptions.DbUrl) ?? string.Empty;
            var skipConfirm = parseResult.GetValue(Yes);
            var dialect = DatabaseOptions.ResolveDialect(
                parseResult.GetValue(DatabaseOptions.Dialect), url);

            if (string.IsNullOrEmpty(url))
            {
                await stderr.WriteLineAsync("error: --db-url is required (or set DBOS_SYSTEM_JDBC_URL).").ConfigureAwait(false);
                return 2;
            }

            if (!skipConfirm)
            {
                await stdout.WriteAsync(
                    "This command resets your DBOS system database, deleting metadata about " +
                    "past workflows and steps. Are you sure you want to proceed? [y/N] ").ConfigureAwait(false);
                var answer = Console.ReadLine()?.Trim();
                if (!string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase))
                {
                    await stdout.WriteLineAsync("System database reset cancelled.").ConfigureAwait(false);
                    return 0;
                }
            }

            return dialect switch
            {
                DatabaseOptions.DialectKind.Postgres => await ResetPostgresAsync(url, stdout, ct).ConfigureAwait(false),
                DatabaseOptions.DialectKind.Sqlite => await ResetSqliteAsync(url, stdout, stderr).ConfigureAwait(false),
                _ => 1,
            };
        });

        return cmd;
    }

    private static async Task<int> ResetPostgresAsync(string url, TextWriter stdout, CancellationToken ct)
    {
        var builder = new NpgsqlConnectionStringBuilder(url);
        var dbName = builder.Database
            ?? throw new InvalidOperationException("Connection string is missing a Database value.");

        builder.Database = "postgres";
        await using var conn = new NpgsqlConnection(builder.ConnectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        await ExecAsync(conn, $"DROP DATABASE IF EXISTS \"{dbName}\" WITH (FORCE)", ct).ConfigureAwait(false);
        await ExecAsync(conn, $"CREATE DATABASE \"{dbName}\"", ct).ConfigureAwait(false);

        await stdout.WriteLineAsync($"System database {dbName} has been reset successfully.").ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> ResetSqliteAsync(string url, TextWriter stdout, TextWriter stderr)
    {
        var builder = new SqliteConnectionStringBuilder(url);
        var path = builder.DataSource;
        if (string.IsNullOrEmpty(path) || string.Equals(path, ":memory:", StringComparison.Ordinal))
        {
            await stderr.WriteLineAsync(
                "error: cannot reset an in-memory SQLite database — there is no file to delete.").ConfigureAwait(false);
            return 1;
        }

        SqliteConnection.ClearAllPools();
        TryDelete(path);
        TryDelete(path + "-wal");
        TryDelete(path + "-shm");

        await stdout.WriteLineAsync($"System database {path} has been reset successfully.").ConfigureAwait(false);
        return 0;
    }

    private static async Task ExecAsync(NpgsqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { /* best-effort */ }
    }
}
