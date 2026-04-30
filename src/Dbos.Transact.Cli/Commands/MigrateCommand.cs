using System.CommandLine;
using Dbos.Transact.Migrations;

namespace Dbos.Transact.Cli.Commands;

internal static class MigrateCommand
{
    public static Command Build()
    {
        var cmd = new Command("migrate", "Create the DBOS system tables.");
        DatabaseOptions.AddTo(cmd);

        cmd.SetAction(async (parseResult, ct) =>
        {
            var stdout = parseResult.InvocationConfiguration.Output;
            var stderr = parseResult.InvocationConfiguration.Error;

            var url = parseResult.GetValue(DatabaseOptions.DbUrl) ?? string.Empty;
            var schema = parseResult.GetValue(DatabaseOptions.Schema) ?? Constants.DbSchema;
            var dialect = DatabaseOptions.ResolveDialect(
                parseResult.GetValue(DatabaseOptions.Dialect), url);

            if (string.IsNullOrEmpty(url))
            {
                await stderr.WriteLineAsync("error: --db-url is required (or set DBOS_SYSTEM_JDBC_URL).").ConfigureAwait(false);
                return 2;
            }

            await stdout.WriteLineAsync("Starting DBOS migrations").ConfigureAwait(false);
            await stdout.WriteLineAsync($"  Dialect: {dialect}").ConfigureAwait(false);
            await stdout.WriteLineAsync($"  Connection: {Redact(url)}").ConfigureAwait(false);
            if (dialect == DatabaseOptions.DialectKind.Postgres)
                await stdout.WriteLineAsync($"  Schema: {schema}").ConfigureAwait(false);

            await using var conn = DatabaseOptions.OpenConnection(dialect, url);
            var manager = new MigrationManager(
                conn,
                dialect == DatabaseOptions.DialectKind.Postgres
                    ? MigrationManager.SqlDialect.Postgres
                    : MigrationManager.SqlDialect.Sqlite,
                schema);
            await manager.RunAsync(ct).ConfigureAwait(false);

            await stdout.WriteLineAsync("Migrations applied successfully.").ConfigureAwait(false);
            return 0;
        });

        return cmd;
    }

    /// <summary>Strips obvious password fields from a connection string for logging.</summary>
    internal static string Redact(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString)) return connectionString;
        var parts = connectionString.Split(';');
        for (int i = 0; i < parts.Length; i++)
        {
            var eq = parts[i].IndexOf('=');
            if (eq < 0) continue;
            var key = parts[i][..eq].Trim();
            if (key.Equals("Password", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("Pwd", StringComparison.OrdinalIgnoreCase))
                parts[i] = key + "=***";
        }
        return string.Join(';', parts);
    }
}
