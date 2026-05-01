using Dbos.Transact.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Dbos.Transact.Conformance;

/// <summary>
/// xUnit class fixture that starts a single Postgres container for all conformance tests
/// and migrates the <c>dbos_conformance</c> schema once.
/// </summary>
public sealed class ConformanceFixture : IAsyncLifetime
{
    internal const string Schema = "dbos_conformance";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithDatabase("dbos_conformance")
        .WithUsername("dbos")
        .WithPassword("dbos")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var conn = new NpgsqlConnection(ConnectionString);
        var mgr = new MigrationManager(conn, MigrationManager.SqlDialect.Postgres, Schema);
        await mgr.RunAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

/// <summary>xUnit collection definition that shares one <see cref="ConformanceFixture"/> across all conformance tests.</summary>
[CollectionDefinition("Conformance")]
public sealed class ConformanceGroup : ICollectionFixture<ConformanceFixture>;
