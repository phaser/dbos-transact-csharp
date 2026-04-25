using Testcontainers.PostgreSql;

namespace Dbos.Transact.Tests.Fixtures;

/// <summary>
/// xUnit class fixture that starts a PostgreSQL container via Testcontainers.NET.
/// Share across a test class with <c>IClassFixture&lt;PostgresFixture&gt;</c>.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithDatabase("dbos_test")
        .WithUsername("dbos")
        .WithPassword("dbos")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
