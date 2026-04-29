using Dbos.Transact.Database;
using Dbos.Transact.Json;

namespace Dbos.Transact;

/// <summary>
/// Fluent builder for <see cref="Dbos"/>. A dialect-specific extension method
/// (e.g. <c>UsePostgres</c>, <c>UseSqlite</c>) registers the system database factory
/// and migration runner before <see cref="Build"/> is called.
/// </summary>
public sealed class DbosBuilder
{
    private DbosOptions _options;
    private Func<IDbosSerializer, SystemDatabase>? _systemDatabaseFactory;
    private Func<DbosOptions, CancellationToken, Task>? _migrationRunner;

    internal DbosBuilder(DbosOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>The current options snapshot. Mutating this property has no effect; use <see cref="WithOptions(Func{DbosOptions, DbosOptions})"/>.</summary>
    public DbosOptions Options => _options;

    /// <summary>Replaces the current options with the result of <paramref name="map"/>.</summary>
    public DbosBuilder WithOptions(Func<DbosOptions, DbosOptions> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        _options = map(_options);
        return this;
    }

    /// <summary>Replaces the current options.</summary>
    public DbosBuilder WithOptions(DbosOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        return this;
    }

    /// <summary>
    /// Registers a system database factory and (optionally) a migration runner for the chosen dialect.
    /// Called by dialect-extension methods such as <c>UsePostgres</c>/<c>UseSqlite</c>.
    /// </summary>
    public DbosBuilder UseSystemDatabase(
        Func<IDbosSerializer, SystemDatabase> systemDatabaseFactory,
        Func<DbosOptions, CancellationToken, Task>? migrationRunner = null)
    {
        ArgumentNullException.ThrowIfNull(systemDatabaseFactory);
        _systemDatabaseFactory = systemDatabaseFactory;
        _migrationRunner = migrationRunner;
        return this;
    }

    /// <summary>
    /// Builds a new <see cref="Dbos"/> instance. The instance is not yet launched —
    /// register workflows and queues, then call <see cref="Dbos.LaunchAsync"/>.
    /// </summary>
    public Dbos Build()
    {
        if (_systemDatabaseFactory is null)
            throw new InvalidOperationException(
                "DbosBuilder requires a system database. Call UsePostgres(...) or UseSqlite(...) before Build().");

        return new Dbos(_options, _systemDatabaseFactory, _migrationRunner);
    }
}
