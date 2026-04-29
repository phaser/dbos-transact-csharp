using Dbos.Transact;
using Dbos.Transact.Migrations;
using Dbos.Transact.Sqlite.Database;
using Microsoft.Data.Sqlite;

namespace Dbos.Transact.Sqlite;

/// <summary>
/// Builder extensions that configure <see cref="Dbos"/> for a SQLite system database.
/// </summary>
public static class DbosSqliteExtensions
{
    /// <summary>
    /// Wires a SQLite-backed <see cref="SqliteSystemDatabase"/> into the builder and registers a
    /// migration runner. Stores <paramref name="connectionString"/> on the options for traceability.
    /// </summary>
    public static DbosBuilder UseSqlite(this DbosBuilder builder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        builder.WithOptions(o => o with { DatabaseUrl = connectionString });

        return builder.UseSystemDatabase(
            systemDatabaseFactory: serializer => new SqliteSystemDatabase(connectionString, serializer),
            migrationRunner: async (_, ct) =>
            {
                await using var conn = new SqliteConnection(connectionString);
                var manager = new MigrationManager(conn, MigrationManager.SqlDialect.Sqlite);
                await manager.RunAsync(ct).ConfigureAwait(false);
            });
    }
}
