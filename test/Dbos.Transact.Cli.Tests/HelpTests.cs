namespace Dbos.Transact.Cli.Tests;

public class HelpTests
{
    [Fact]
    public async Task Root_Help_ListsAllSubcommands()
    {
        var r = await CliRunner.RunAsync("--help");
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("DBOS CLI", r.Stdout, StringComparison.Ordinal);
        Assert.Contains("migrate", r.Stdout, StringComparison.Ordinal);
        Assert.Contains("reset", r.Stdout, StringComparison.Ordinal);
        Assert.Contains("workflow", r.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Migrate_Help_ListsExpectedOptions()
    {
        var r = await CliRunner.RunAsync("migrate", "--help");
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("--db-url", r.Stdout, StringComparison.Ordinal);
        Assert.Contains("--schema", r.Stdout, StringComparison.Ordinal);
        Assert.Contains("--dialect", r.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Workflow_Help_ListsAllSubcommands()
    {
        var r = await CliRunner.RunAsync("workflow", "--help");
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("list", r.Stdout, StringComparison.Ordinal);
        Assert.Contains("get", r.Stdout, StringComparison.Ordinal);
        Assert.Contains("cancel", r.Stdout, StringComparison.Ordinal);
        Assert.Contains("resume", r.Stdout, StringComparison.Ordinal);
        Assert.Contains("steps", r.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reset_Help_HasYesFlag()
    {
        var r = await CliRunner.RunAsync("reset", "--help");
        Assert.Equal(0, r.ExitCode);
        Assert.Contains("--yes", r.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Migrate_MissingDbUrl_FailsWithExitCode2()
    {
        // Ensure env var is not set so default is empty.
        var prev = Environment.GetEnvironmentVariable("DBOS_SYSTEM_JDBC_URL");
        Environment.SetEnvironmentVariable("DBOS_SYSTEM_JDBC_URL", null);
        try
        {
            var r = await CliRunner.RunAsync("migrate");
            Assert.Equal(2, r.ExitCode);
            Assert.Contains("--db-url", r.Stderr, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DBOS_SYSTEM_JDBC_URL", prev);
        }
    }

    [Fact]
    public void Redact_HidesPasswordField()
    {
        var s = Commands.MigrateCommand.Redact("Host=localhost;User=u;Password=secret;Database=d");
        Assert.DoesNotContain("secret", s, StringComparison.Ordinal);
        Assert.Contains("Password=***", s, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Data Source=foo.sqlite;", "Sqlite")]
    [InlineData("Filename=foo.db;", "Sqlite")]
    [InlineData("/tmp/foo.sqlite", "Sqlite")]
    [InlineData("/tmp/foo.db", "Sqlite")]
    [InlineData("Host=localhost;Username=u;Password=p;Database=d", "Postgres")]
    [InlineData("Server=localhost;Port=5432", "Postgres")]
    public void ResolveDialect_AutodetectsFromUrl(string url, string expected)
    {
        var actual = DatabaseOptions.ResolveDialect(null, url);
        Assert.Equal(expected, actual.ToString());
    }

    [Fact]
    public void ResolveDialect_ExplicitOverridesAutodetect()
    {
        // URL "looks" like SQLite but explicit Postgres wins.
        var d = DatabaseOptions.ResolveDialect(DatabaseOptions.DialectKind.Postgres, "Data Source=foo.sqlite");
        Assert.Equal(DatabaseOptions.DialectKind.Postgres, d);
    }
}
