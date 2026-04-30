using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Dbos.Transact.Database;
using Dbos.Transact.Execution;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Admin;

/// <summary>
/// HTTP admin server, port of Java's <c>AdminServer</c>. Uses <see cref="HttpListener"/> —
/// the BCL equivalent of the Java <c>com.sun.net.httpserver.HttpServer</c> the upstream uses,
/// so the core library does not pull in ASP.NET Core. Endpoint surface and HTTP status codes
/// match Java exactly where the underlying capability is implemented in this port.
/// </summary>
public sealed class AdminServer : IAsyncDisposable
{
    private static readonly Regex WorkflowPathPattern = new(
        @"^/workflows/([^/]+)(/[^/]*)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly DbosExecutor _executor;
    private readonly SystemDatabase _db;
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    /// <summary>Creates an admin server bound to the given port. Call <see cref="Start"/> to listen.</summary>
    public AdminServer(int port, DbosExecutor executor, SystemDatabase db)
    {
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(db);
        if (port < 0 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be 0-65535.");

        _executor = executor;
        _db = db;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    }

    /// <summary>The base URI the server is listening on (only valid after <see cref="Start"/>).</summary>
    public Uri? BaseUri { get; private set; }

    /// <summary>Starts the listener and the request-dispatch loop on a background task.</summary>
    public void Start()
    {
        if (_loop is not null) return;
        _listener.Start();
        BaseUri = new Uri(_listener.Prefixes.First().TrimEnd('/'));
        _loop = Task.Run(() => DispatchLoopAsync(_cts.Token));
    }

    public async ValueTask DisposeAsync()
    {
        if (_loop is null)
        {
            _cts.Dispose();
            return;
        }

        await _cts.CancelAsync().ConfigureAwait(false);
        try { _listener.Stop(); } catch { /* best-effort */ }

        try { await _loop.ConfigureAwait(false); }
        catch (OperationCanceledException) { /* expected */ }

        _listener.Close();
        _cts.Dispose();
    }

    // ── Dispatch loop ─────────────────────────────────────────────────────────

    private async Task DispatchLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException) { return; }
            catch (HttpListenerException) { return; }

            // Handle each request on its own task to allow concurrency.
            _ = Task.Run(() => HandleSafelyAsync(context, ct), ct);
        }
    }

    private async Task HandleSafelyAsync(HttpListenerContext context, CancellationToken ct)
    {
        try
        {
            await DispatchAsync(context, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await SendTextAsync(context, 500, ex.Message).ConfigureAwait(false);
        }
        finally
        {
            try { context.Response.Close(); } catch { /* best-effort */ }
        }
    }

    private async Task DispatchAsync(HttpListenerContext context, CancellationToken ct)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";

        switch (path)
        {
            case "/dbos-healthz": await HealthCheckAsync(context).ConfigureAwait(false); return;
            case "/dbos-workflow-recovery": await WorkflowRecoveryAsync(context, ct).ConfigureAwait(false); return;
            case "/deactivate": await DeactivateAsync(context).ConfigureAwait(false); return;
            case "/dbos-workflow-queues-metadata": await WorkflowQueuesMetadataAsync(context).ConfigureAwait(false); return;
            case "/queues": await ListQueuedWorkflowsAsync(context, ct).ConfigureAwait(false); return;
            case "/workflows": await ListWorkflowsAsync(context, ct).ConfigureAwait(false); return;
        }

        var match = WorkflowPathPattern.Match(path);
        if (match.Success)
        {
            var workflowId = match.Groups[1].Value;
            var subPath = match.Groups[2].Success ? match.Groups[2].Value : null;

            if (subPath is null)
            {
                await GetWorkflowAsync(context, workflowId, ct).ConfigureAwait(false);
                return;
            }

            switch (subPath)
            {
                case "/steps": await ListStepsAsync(context, workflowId, ct).ConfigureAwait(false); return;
                case "/cancel": await CancelAsync(context, workflowId, ct).ConfigureAwait(false); return;
                case "/resume": await ResumeAsync(context, workflowId, ct).ConfigureAwait(false); return;
                case "/fork": await ForkAsync(context, workflowId, ct).ConfigureAwait(false); return;
            }
        }

        context.Response.StatusCode = 404;
    }

    // ── Static endpoints ──────────────────────────────────────────────────────

    private static async Task HealthCheckAsync(HttpListenerContext context) =>
        await SendJsonAsync(context, 200, """{"status":"healthy"}""").ConfigureAwait(false);

    private async Task WorkflowRecoveryAsync(HttpListenerContext context, CancellationToken ct)
    {
        if (!await EnsurePostJsonAsync(context).ConfigureAwait(false)) return;

        var executorIds = await ReadJsonAsync<List<string>>(context).ConfigureAwait(false)
            ?? new List<string>();
        var ids = await _executor.RecoverPendingWorkflowsAsync(executorIds, ct).ConfigureAwait(false);
        await SendMappedJsonAsync(context, 200, ids).ConfigureAwait(false);
    }

    private async Task DeactivateAsync(HttpListenerContext context)
    {
        _executor.DeactivateLifecycleListeners();
        await SendTextAsync(context, 200, "deactivated").ConfigureAwait(false);
    }

    private async Task WorkflowQueuesMetadataAsync(HttpListenerContext context)
    {
        var queues = _executor.GetQueues();
        await SendMappedJsonAsync(context, 200, queues).ConfigureAwait(false);
    }

    private async Task ListWorkflowsAsync(HttpListenerContext context, CancellationToken ct)
    {
        if (!await EnsurePostJsonAsync(context).ConfigureAwait(false)) return;

        var request = await ReadJsonAsync<ListWorkflowsRequest>(context).ConfigureAwait(false);
        if (request is null) { context.Response.StatusCode = 400; return; }

        var workflows = await _db.ListWorkflowsAsync(request.AsInput(), ct).ConfigureAwait(false);
        var output = workflows.Select(WorkflowsOutput.Of).ToArray();
        await SendMappedJsonAsync(context, 200, output).ConfigureAwait(false);
    }

    private async Task ListQueuedWorkflowsAsync(HttpListenerContext context, CancellationToken ct)
    {
        if (!await EnsurePostJsonAsync(context).ConfigureAwait(false)) return;

        var request = await ReadJsonAsync<ListQueuedWorkflowsRequest>(context).ConfigureAwait(false);
        if (request is null) { context.Response.StatusCode = 400; return; }

        var workflows = await _db.ListWorkflowsAsync(request.AsInput(), ct).ConfigureAwait(false);
        var output = workflows.Select(WorkflowsOutput.Of).ToArray();
        await SendMappedJsonAsync(context, 200, output).ConfigureAwait(false);
    }

    // ── Workflow-specific endpoints ───────────────────────────────────────────

    private async Task GetWorkflowAsync(HttpListenerContext context, string workflowId, CancellationToken ct)
    {
        var input = new ListWorkflowsInput(workflowId);
        var workflows = await _db.ListWorkflowsAsync(input, ct).ConfigureAwait(false);
        if (workflows.Count == 0)
        {
            await SendTextAsync(context, 404, "Workflow not found").ConfigureAwait(false);
            return;
        }
        await SendMappedJsonAsync(context, 200, WorkflowsOutput.Of(workflows[0])).ConfigureAwait(false);
    }

    private async Task ListStepsAsync(HttpListenerContext context, string workflowId, CancellationToken ct)
    {
        var steps = await _db.ListWorkflowStepsAsync(workflowId, loadOutput: true, limit: null, offset: null, ct)
            .ConfigureAwait(false);
        await SendMappedJsonAsync(context, 200, steps.Select(StepOutput.Of).ToArray()).ConfigureAwait(false);
    }

    private async Task CancelAsync(HttpListenerContext context, string workflowId, CancellationToken ct)
    {
        if (!EnsurePost(context)) return;
        await _db.CancelWorkflowsAsync([workflowId], ct).ConfigureAwait(false);
        context.Response.StatusCode = 204;
    }

    private async Task ResumeAsync(HttpListenerContext context, string workflowId, CancellationToken ct)
    {
        if (!EnsurePost(context)) return;
        await _db.ResumeWorkflowsAsync([workflowId], queueName: null, ct).ConfigureAwait(false);
        context.Response.StatusCode = 204;
    }

    private async Task ForkAsync(HttpListenerContext context, string workflowId, CancellationToken ct)
    {
        if (!await EnsurePostJsonAsync(context).ConfigureAwait(false)) return;

        var request = await ReadJsonAsync<ForkRequest>(context).ConfigureAwait(false)
            ?? new ForkRequest(null, null, null);
        var startStep = request.StartStep ?? 0;

        var options = new ForkOptions(
            ForkedWorkflowId: request.NewWorkflowId,
            ApplicationVersion: request.ApplicationVersion,
            Timeout: null,
            QueueName: null,
            QueuePartitionKey: null);

        var forkedId = await _db.ForkWorkflowAsync(workflowId, startStep, options, ct).ConfigureAwait(false);
        await SendMappedJsonAsync(context, 200, new ForkResponse(forkedId)).ConfigureAwait(false);
    }

    // ── Method/content guards ─────────────────────────────────────────────────

    private static bool EnsurePost(HttpListenerContext context)
    {
        if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 405;
            return false;
        }
        return true;
    }

    private static async Task<bool> EnsurePostJsonAsync(HttpListenerContext context)
    {
        if (!EnsurePost(context)) return false;

        var contentType = context.Request.ContentType;
        if (string.IsNullOrEmpty(contentType) ||
            !contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            await SendTextAsync(context, 415, "Unsupported Media Type").ConfigureAwait(false);
            return false;
        }
        return true;
    }

    // ── Body / response helpers ───────────────────────────────────────────────

    private static async Task<T?> ReadJsonAsync<T>(HttpListenerContext context)
    {
        return await JsonSerializer.DeserializeAsync<T>(context.Request.InputStream, JsonOptions)
            .ConfigureAwait(false);
    }

    private static async Task SendTextAsync(HttpListenerContext context, int statusCode, string text)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "text/plain";
        var bytes = Encoding.UTF8.GetBytes(text);
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
    }

    private static async Task SendJsonAsync(HttpListenerContext context, int statusCode, string json)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
    }

    private static Task SendMappedJsonAsync(HttpListenerContext context, int statusCode, object value) =>
        SendJsonAsync(context, statusCode, JsonSerializer.Serialize(value, JsonOptions));

    // ── Request / response DTOs ───────────────────────────────────────────────

    internal sealed record ForkRequest(
        [property: JsonPropertyName("start_step")] int? StartStep,
        [property: JsonPropertyName("new_workflow_id")] string? NewWorkflowId,
        [property: JsonPropertyName("application_version")] string? ApplicationVersion);

    internal sealed record ForkResponse(
        [property: JsonPropertyName("workflow_id")] string WorkflowId);

    // ── Convenience for tests / callers using CultureInvariant numeric format ─

    internal static string EpochMsString(long? ms) =>
        ms.HasValue ? ms.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
}
