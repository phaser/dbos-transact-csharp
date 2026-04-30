using System.CommandLine;

namespace Dbos.Transact.Cli.Tests;

/// <summary>
/// Helper that invokes the DBOS CLI in-process, capturing stdout and stderr.
/// Uses per-invocation <c>InvocationConfiguration.Output/.Error</c> writers so concurrent
/// xUnit tests don't trample each other's redirection.
/// </summary>
internal static class CliRunner
{
    public sealed record Result(int ExitCode, string Stdout, string Stderr);

    public static async Task<Result> RunAsync(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var root = Program.BuildRootCommand();
        var parseResult = root.Parse(args);

        // Override output for this invocation only. Help and parse-error messages also use these.
        parseResult.InvocationConfiguration.Output = stdout;
        parseResult.InvocationConfiguration.Error = stderr;

        var exit = await parseResult.InvokeAsync().ConfigureAwait(false);
        return new Result(exit, stdout.ToString(), stderr.ToString());
    }
}
