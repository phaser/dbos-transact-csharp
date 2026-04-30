using System.CommandLine;
using Dbos.Transact.Cli.Commands;

namespace Dbos.Transact.Cli;

internal static class Program
{
    public static Task<int> Main(string[] args) =>
        BuildRootCommand().Parse(args).InvokeAsync();

    /// <summary>Builds the root command tree. Public for reuse from tests.</summary>
    public static RootCommand BuildRootCommand()
    {
        var root = new RootCommand("DBOS CLI — manage DBOS workflows and the system database.");
        root.Subcommands.Add(MigrateCommand.Build());
        root.Subcommands.Add(ResetCommand.Build());
        root.Subcommands.Add(WorkflowCommand.Build());
        return root;
    }
}
