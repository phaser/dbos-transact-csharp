using Dbos.Transact;
using Dbos.Transact.Migrations;
using Dbos.Transact.Postgres.Database;
using Npgsql;

namespace Dbos.Transact.Postgres;

/// <summary>
/// Builder extensions that configure <see cref="Dbos"/> for a PostgreSQL system database.
/// </summary>
public static class DbosPostgresExtensions
{
    /// <summary>
    /// Wires a Postgres-backed <see cref="PostgresSystemDatabase"/> into the builder and registers a
    /// migration runner. Stores <paramref name="connectionString"/> and <paramref name="schema"/> on
    /// the options for traceability.
    /// </summary>
    public static DbosBuilder UsePostgres(
        this DbosBuilder builder,
        string connectionString,
        string schema = Constants.DbSchema)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        ArgumentException.ThrowIfNullOrEmpty(schema);

        builder.WithOptions(o => o with { DatabaseUrl = connectionString, DatabaseSchema = schema });

        return builder.UseSystemDatabase(
            systemDatabaseFactory: serializer =>
                new PostgresSystemDatabase(connectionString, schema, serializer),
            migrationRunner: async (_, ct) =>
            {
                await using var conn = new NpgsqlConnection(connectionString);
                var manager = new MigrationManager(conn, MigrationManager.SqlDialect.Postgres, schema);
                await manager.RunAsync(ct).ConfigureAwait(false);
            });
    }
}
