using System.CommandLine;
using System.Text.Json;
using Dbos.Transact.Database;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Cli.Commands;

internal static class WorkflowCommand
{
    public static Command Build()
    {
        var cmd = new Command("workflow", "Manage DBOS workflows.")
        {
            Aliases = { "wf" },
        };

        cmd.Subcommands.Add(BuildList());
        cmd.Subcommands.Add(BuildGet());
        cmd.Subcommands.Add(BuildSteps());
        cmd.Subcommands.Add(BuildCancel());
        cmd.Subcommands.Add(BuildResume());

        return cmd;
    }

    // ── list ──────────────────────────────────────────────────────────────────

    private static readonly Option<int> Limit = new("--limit", "-l")
    {
        Description = "Maximum number of workflows to return.",
        DefaultValueFactory = _ => 10,
    };

    private static readonly Option<int> Offset = new("--offset", "-o")
    {
        Description = "Pagination offset.",
        DefaultValueFactory = _ => 0,
    };

    private static readonly Option<string?> StatusFilter = new("--status", "-S")
    {
        Description =
            "Filter by status (PENDING, SUCCESS, ERROR, ENQUEUED, CANCELLED, MAX_RECOVERY_ATTEMPTS_EXCEEDED).",
    };

    private static readonly Option<string?> NameFilter = new("--name", "-n")
    {
        Description = "Filter by workflow name.",
    };

    private static readonly Option<bool> SortDesc = new("--sort-desc", "-d")
    {
        Description = "Sort the results in descending order.",
    };

    private static Command BuildList()
    {
        var cmd = new Command("list", "List workflows.");
        DatabaseOptions.AddTo(cmd);
        cmd.Options.Add(Limit);
        cmd.Options.Add(Offset);
        cmd.Options.Add(StatusFilter);
        cmd.Options.Add(NameFilter);
        cmd.Options.Add(SortDesc);

        cmd.SetAction(async (parseResult, ct) =>
        {
            var stdout = parseResult.InvocationConfiguration.Output;
            var (db, _) = await OpenAsync(parseResult, ct).ConfigureAwait(false);
            await using var _db = db;

            var input = new ListWorkflowsInput(
                WorkflowIds: null,
                Status: ParseStatus(parseResult.GetValue(StatusFilter)),
                StartTime: null,
                EndTime: null,
                WorkflowName: ToSingletonList(parseResult.GetValue(NameFilter)),
                ClassName: null,
                InstanceName: null,
                ApplicationVersion: null,
                AuthenticatedUser: null,
                Limit: parseResult.GetValue(Limit),
                Offset: parseResult.GetValue(Offset),
                SortDesc: parseResult.GetValue(SortDesc),
                WorkflowIdPrefix: null,
                LoadInput: false,
                LoadOutput: false,
                QueueName: null,
                QueuesOnly: false,
                ExecutorIds: null,
                ForkedFrom: null,
                ParentWorkflowId: null,
                WasForkedFrom: null,
                HasParent: null);

            var rows = await db.ListWorkflowsAsync(input, ct).ConfigureAwait(false);
            await stdout.WriteLineAsync(PrettyJson(rows)).ConfigureAwait(false);
            return 0;
        });

        return cmd;
    }

    // ── get ───────────────────────────────────────────────────────────────────

    private static Command BuildGet()
    {
        var workflowId = new Argument<string>("workflowId") { Description = "Workflow ID to retrieve." };
        var cmd = new Command("get", "Retrieve the status of a workflow.");
        DatabaseOptions.AddTo(cmd);
        cmd.Arguments.Add(workflowId);

        cmd.SetAction(async (parseResult, ct) =>
        {
            var stdout = parseResult.InvocationConfiguration.Output;
            var stderr = parseResult.InvocationConfiguration.Error;

            var (db, _) = await OpenAsync(parseResult, ct).ConfigureAwait(false);
            await using var _db = db;

            var id = parseResult.GetValue(workflowId)!;
            var status = await db.GetWorkflowStatusAsync(id, ct).ConfigureAwait(false);
            if (status is null)
            {
                await stderr.WriteLineAsync($"Failed to retrieve workflow {id}").ConfigureAwait(false);
                return 1;
            }

            await stdout.WriteLineAsync(PrettyJson(status)).ConfigureAwait(false);
            return 0;
        });

        return cmd;
    }

    // ── steps ─────────────────────────────────────────────────────────────────

    private static Command BuildSteps()
    {
        var workflowId = new Argument<string>("workflowId") { Description = "Workflow ID whose steps to list." };
        var cmd = new Command("steps", "List the steps of a workflow.");
        DatabaseOptions.AddTo(cmd);
        cmd.Arguments.Add(workflowId);

        cmd.SetAction(async (parseResult, ct) =>
        {
            var stdout = parseResult.InvocationConfiguration.Output;
            var (db, _) = await OpenAsync(parseResult, ct).ConfigureAwait(false);
            await using var _db = db;

            var id = parseResult.GetValue(workflowId)!;
            var steps = await db.ListWorkflowStepsAsync(id, loadOutput: true, limit: null, offset: null, ct)
                .ConfigureAwait(false);
            await stdout.WriteLineAsync(PrettyJson(steps)).ConfigureAwait(false);
            return 0;
        });

        return cmd;
    }

    // ── cancel ────────────────────────────────────────────────────────────────

    private static Command BuildCancel()
    {
        var workflowId = new Argument<string>("workflowId") { Description = "Workflow ID to cancel." };
        var cmd = new Command("cancel", "Cancel a workflow.");
        DatabaseOptions.AddTo(cmd);
        cmd.Arguments.Add(workflowId);

        cmd.SetAction(async (parseResult, ct) =>
        {
            var stdout = parseResult.InvocationConfiguration.Output;
            var (db, _) = await OpenAsync(parseResult, ct).ConfigureAwait(false);
            await using var _db = db;

            var id = parseResult.GetValue(workflowId)!;
            await db.CancelWorkflowsAsync([id], ct).ConfigureAwait(false);
            await stdout.WriteLineAsync($"Successfully cancelled workflow {id}.").ConfigureAwait(false);
            return 0;
        });

        return cmd;
    }

    // ── resume ────────────────────────────────────────────────────────────────

    private static Command BuildResume()
    {
        var workflowId = new Argument<string>("workflowId") { Description = "Workflow ID to resume." };
        var cmd = new Command("resume", "Resume a cancelled or failed workflow.");
        DatabaseOptions.AddTo(cmd);
        cmd.Arguments.Add(workflowId);

        cmd.SetAction(async (parseResult, ct) =>
        {
            var stdout = parseResult.InvocationConfiguration.Output;
            var (db, _) = await OpenAsync(parseResult, ct).ConfigureAwait(false);
            await using var _db = db;

            var id = parseResult.GetValue(workflowId)!;
            await db.ResumeWorkflowsAsync([id], queueName: null, ct).ConfigureAwait(false);

            var status = await db.GetWorkflowStatusAsync(id, ct).ConfigureAwait(false);
            await stdout.WriteLineAsync(status is null ? "{}" : PrettyJson(status)).ConfigureAwait(false);
            return 0;
        });

        return cmd;
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static async Task<(SystemDatabase Db, DatabaseOptions.DialectKind Dialect)> OpenAsync(
        System.CommandLine.ParseResult parseResult, CancellationToken ct)
    {
        var url = parseResult.GetValue(DatabaseOptions.DbUrl) ?? string.Empty;
        var schema = parseResult.GetValue(DatabaseOptions.Schema) ?? Constants.DbSchema;
        var dialect = DatabaseOptions.ResolveDialect(
            parseResult.GetValue(DatabaseOptions.Dialect), url);

        if (string.IsNullOrEmpty(url))
            throw new InvalidOperationException("--db-url is required (or set DBOS_SYSTEM_JDBC_URL).");

        var db = DatabaseOptions.CreateSystemDatabase(dialect, url, schema);
        await db.StartAsync(ct).ConfigureAwait(false);
        return (db, dialect);
    }

    private static WorkflowState[]? ParseStatus(string? input)
    {
        if (string.IsNullOrEmpty(input)) return null;
        if (Enum.TryParse<WorkflowState>(input, ignoreCase: true, out var s))
            return [s];
        return [WorkflowStateExtensions.ParseDbStatus(input)];
    }

    private static string[]? ToSingletonList(string? value) =>
        string.IsNullOrEmpty(value) ? null : [value];

    private static readonly JsonSerializerOptions PrettyOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private static string PrettyJson(object? value) => JsonSerializer.Serialize(value, PrettyOptions);
}
