using System.Reflection;
using Dbos.Transact.Migrations;

namespace Dbos.Transact.Tests.Migrations;

public class MigrationManagerTests
{
    [Fact]
    public void SplitStatements_SingleStatement_ReturnsOne()
    {
        var result = MigrationManager.SplitStatements("SELECT 1");
        Assert.Single(result);
        Assert.Equal("SELECT 1", result[0]);
    }

    [Fact]
    public void SplitStatements_MultipleStatements_ReturnsAll()
    {
        var sql = "CREATE TABLE foo (id INTEGER);\nCREATE TABLE bar (id INTEGER);";
        var result = MigrationManager.SplitStatements(sql);
        Assert.Equal(2, result.Count);
        Assert.Contains("CREATE TABLE foo (id INTEGER)", result);
        Assert.Contains("CREATE TABLE bar (id INTEGER)", result);
    }

    [Fact]
    public void SplitStatements_EmptyParts_AreSkipped()
    {
        var sql = "SELECT 1;\n\n;SELECT 2;";
        var result = MigrationManager.SplitStatements(sql);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void SplitStatements_SemicolonInLineComment_IsIgnored()
    {
        // A semicolon inside a -- comment must not produce a spurious statement.
        var sql = "-- comment; no-op\nSELECT 1;";
        var result = MigrationManager.SplitStatements(sql);
        Assert.Single(result);
        Assert.Equal("SELECT 1", result[0]);
    }

    [Fact]
    public void EmbeddedResources_Postgres_Has19Scripts()
    {
        var assembly = typeof(MigrationManager).Assembly;
        var count = assembly.GetManifestResourceNames()
            .Count(n => n.StartsWith("Dbos.Transact.Migrations.Sql.Postgres.", StringComparison.Ordinal)
                     && n.EndsWith(".sql", StringComparison.Ordinal));
        Assert.Equal(19, count);
    }

    [Fact]
    public void EmbeddedResources_Sqlite_Has19Scripts()
    {
        var assembly = typeof(MigrationManager).Assembly;
        var count = assembly.GetManifestResourceNames()
            .Count(n => n.StartsWith("Dbos.Transact.Migrations.Sql.Sqlite.", StringComparison.Ordinal)
                     && n.EndsWith(".sql", StringComparison.Ordinal));
        Assert.Equal(19, count);
    }

    [Fact]
    public void EmbeddedResources_AllPostgresSqlNonEmpty()
    {
        var assembly = typeof(MigrationManager).Assembly;
        var resources = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith("Dbos.Transact.Migrations.Sql.Postgres.", StringComparison.Ordinal)
                     && n.EndsWith(".sql", StringComparison.Ordinal));
        foreach (var name in resources)
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new System.IO.StreamReader(stream);
            var content = reader.ReadToEnd();
            Assert.False(string.IsNullOrWhiteSpace(content), $"{name} must not be empty");
        }
    }
}
