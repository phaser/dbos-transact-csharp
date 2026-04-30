using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Dbos.Transact.Conductor.Protocol;
using Dbos.Transact.Database;
using Dbos.Transact.Execution;
using Dbos.Transact.Workflow;

namespace Dbos.Transact.Conductor;

/// <summary>
/// WebSocket client to DBOS Cloud's conductor service. Receives administrative requests
/// (cancel/resume/list/etc.) over a single long-lived connection, dispatches each to the
/// appropriate <see cref="DbosExecutor"/> / <see cref="SystemDatabase"/> handler, and writes
/// back a <see cref="BaseResponse"/> correlated by <c>request_id</c>.
///
/// Mirrors Java's <c>Conductor</c>: connect-loop with reconnect-on-failure, ping/pong heartbeat,
/// per-request handler dispatch via the <see cref="MessageType"/> discriminator. Handlers whose
/// underlying capability is not yet ported respond with a <see cref="BaseResponse"/> error
/// (matches Java behavior for unknown message types).
/// </summary>
public sealed class Conductor : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = null,
    };

    private readonly Uri _url;
    private readonly DbosExecutor _executor;
    private readonly SystemDatabase _db;
    private readonly TimeSpan _pingPeriod;
    private readonly TimeSpan _reconnectDelay;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private Task? _connectLoop;
    private ClientWebSocket? _activeWebSocket;
    private bool _shutdown;
    private bool _disposed;

    private Conductor(Build build)
    {
        ArgumentNullException.ThrowIfNull(build.Executor);
        ArgumentNullException.ThrowIfNull(build.SystemDatabase);
        ArgumentException.ThrowIfNullOrEmpty(build.AppName);
        ArgumentException.ThrowIfNullOrEmpty(build.ConductorKey);

        _executor = build.Executor;
        _db = build.SystemDatabase;
        _pingPeriod = build.PingPeriod;
        _reconnectDelay = build.ReconnectDelay;

        var domain = ResolveDomain(build.Domain);
        _url = new Uri($"{domain}/websocket/{build.AppName}/{build.ConductorKey}");
    }

    private static string ResolveDomain(string? domain)
    {
        if (string.IsNullOrEmpty(domain))
        {
            var fromEnv = Environment.GetEnvironmentVariable("DBOS_DOMAIN");
            domain = string.IsNullOrEmpty(fromEnv?.Trim())
                ? "wss://cloud.dbos.dev"
                : "wss://" + fromEnv.Trim();
            domain += "/conductor/v1alpha1";
        }
        else
        {
            domain = domain.TrimEnd('/');
        }
        return domain;
    }

    /// <summary>The URL the conductor is dialing (for diagnostics / tests).</summary>
    public Uri Url => _url;

    /// <summary>True when the connection loop has at least one open WebSocket. Mainly for tests.</summary>
    public bool IsConnected =>
        _activeWebSocket is { State: WebSocketState.Open };

    /// <summary>Builder for <see cref="Conductor"/>.</summary>
    public sealed class Build
    {
        public DbosExecutor Executor { get; }
        public SystemDatabase SystemDatabase { get; }
        public string AppName { get; }
        public string ConductorKey { get; }
        public string? Domain { get; init; }
        public TimeSpan PingPeriod { get; init; } = TimeSpan.FromSeconds(20);
        public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(1);

        public Build(DbosExecutor executor, SystemDatabase systemDatabase, string appName, string conductorKey)
        {
            Executor = executor;
            SystemDatabase = systemDatabase;
            AppName = appName;
            ConductorKey = conductorKey;
        }

        public Conductor BuildConductor() => new(this);
    }

    /// <summary>
    /// Starts the connect loop on a background task. Idempotent — a second call is a no-op.
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_connectLoop is not null) return;
        _connectLoop = Task.Run(() => ConnectLoopAsync(_cts.Token));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _shutdown = true;

        await _cts.CancelAsync().ConfigureAwait(false);

        var ws = _activeWebSocket;
        if (ws is not null)
        {
            try { ws.Abort(); } catch { /* best-effort */ }
        }

        if (_connectLoop is not null)
        {
            try { await _connectLoop.ConfigureAwait(false); }
            catch (OperationCanceledException) { /* expected */ }
        }

        _cts.Dispose();
        _sendLock.Dispose();
    }

    // ── Connect loop ──────────────────────────────────────────────────────────

    private async Task ConnectLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && !_shutdown)
        {
            ClientWebSocket? ws = null;
            try
            {
                ws = new ClientWebSocket();
                ws.Options.KeepAliveInterval = _pingPeriod;
                await ws.ConnectAsync(_url, ct).ConfigureAwait(false);
                _activeWebSocket = ws;

                await ReceiveLoopAsync(ws, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception)
            {
                // Connection failed or dropped — fall through to reconnect delay below.
            }
            finally
            {
                _activeWebSocket = null;
                try { ws?.Dispose(); } catch { /* best-effort */ }
            }

            if (ct.IsCancellationRequested || _shutdown) return;

            try { await Task.Delay(_reconnectDelay, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[8192];
        var bufferMemory = new ArraySegment<byte>(buffer);
        var messageBuilder = new StringBuilder();

        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await ws.ReceiveAsync(bufferMemory, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (WebSocketException) { return; }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                try
                {
                    await ws.CloseAsync(
                        WebSocketCloseStatus.NormalClosure, null, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch { /* best-effort */ }
                return;
            }

            messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            if (!result.EndOfMessage) continue;

            var text = messageBuilder.ToString();
            messageBuilder.Clear();

            // Dispatch each message on its own task so subsequent receives aren't blocked.
            _ = Task.Run(() => HandleAndRespondAsync(ws, text, ct), ct);
        }
    }

    private async Task HandleAndRespondAsync(ClientWebSocket ws, string text, CancellationToken ct)
    {
        BaseMessage? request;
        try
        {
            request = JsonSerializer.Deserialize<BaseMessage>(text, JsonOptions);
        }
        catch
        {
            return; // malformed payload — log + drop in production; nothing to correlate to
        }

        if (request is null) return;

        BaseResponse response;
        try
        {
            response = await DispatchAsync(request, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            response = new BaseResponse(request.Type, request.RequestId, ex.Message);
        }

        try { await SendAsync(ws, response, ct).ConfigureAwait(false); }
        catch { /* connection probably closed; reconnect loop will handle */ }
    }

    private async Task SendAsync(ClientWebSocket ws, BaseResponse response, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize<object>(response, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct)
                .ConfigureAwait(false);
        }
        finally { _sendLock.Release(); }
    }

    // ── Dispatch ──────────────────────────────────────────────────────────────

    internal async Task<BaseResponse> DispatchAsync(BaseMessage request, CancellationToken ct) =>
        request switch
        {
            ExecutorInfoRequest => HandleExecutorInfo(request),
            CancelRequest cancel => await HandleCancelAsync(cancel, ct).ConfigureAwait(false),
            ResumeRequest resume => await HandleResumeAsync(resume, ct).ConfigureAwait(false),
            DeleteRequest delete => await HandleDeleteAsync(delete, ct).ConfigureAwait(false),
            RecoveryRequest recovery => await HandleRecoveryAsync(recovery, ct).ConfigureAwait(false),
            ListWorkflowsRequest listWf => await HandleListWorkflowsAsync(listWf, ct).ConfigureAwait(false),
            ListQueuedWorkflowsRequest listQ => await HandleListQueuedWorkflowsAsync(listQ, ct).ConfigureAwait(false),
            ListStepsRequest listSteps => await HandleListStepsAsync(listSteps, ct).ConfigureAwait(false),
            GetWorkflowRequest get => await HandleGetWorkflowAsync(get, ct).ConfigureAwait(false),
            ExistPendingWorkflowsRequest exist => await HandleExistPendingWorkflowsAsync(exist, ct).ConfigureAwait(false),
            ListSchedulesRequest listSch => await HandleListSchedulesAsync(listSch, ct).ConfigureAwait(false),
            GetScheduleRequest getSch => await HandleGetScheduleAsync(getSch, ct).ConfigureAwait(false),
            PauseScheduleRequest pause => await HandlePauseScheduleAsync(pause, ct).ConfigureAwait(false),
            ResumeScheduleRequest resumeSch => await HandleResumeScheduleAsync(resumeSch, ct).ConfigureAwait(false),
            _ => new BaseResponse(request.Type, request.RequestId,
                "Message type not implemented in the C# port v1."),
        };

    private ExecutorInfoResponse HandleExecutorInfo(BaseMessage message)
    {
        try
        {
            return new ExecutorInfoResponse(
                message: message,
                executorId: _executor.ExecutorId,
                appVersion: _executor.LatestApplicationVersion ?? string.Empty,
                hostname: Environment.MachineName,
                dbosVersion: typeof(Conductor).Assembly.GetName().Version?.ToString() ?? "unknown",
                executorMetadata: null);
        }
        catch (Exception ex)
        {
            return new ExecutorInfoResponse(message, ex);
        }
    }

    private async Task<BaseResponse> HandleCancelAsync(CancelRequest request, CancellationToken ct)
    {
        var ids = ResolveIds(request.WorkflowIds, request.WorkflowId);
        try
        {
            await _db.CancelWorkflowsAsync(ids, ct).ConfigureAwait(false);
            return new SuccessResponse(request, success: true);
        }
        catch (Exception ex)
        {
            return new SuccessResponse(request, ex);
        }
    }

    private async Task<BaseResponse> HandleResumeAsync(ResumeRequest request, CancellationToken ct)
    {
        var ids = ResolveIds(request.WorkflowIds, request.WorkflowId);
        try
        {
            await _db.ResumeWorkflowsAsync(ids, request.QueueName, ct).ConfigureAwait(false);
            return new SuccessResponse(request, success: true);
        }
        catch (Exception ex)
        {
            return new SuccessResponse(request, ex);
        }
    }

    private async Task<BaseResponse> HandleDeleteAsync(DeleteRequest request, CancellationToken ct)
    {
        var ids = ResolveIds(request.WorkflowIds, request.WorkflowId);
        try
        {
            await _db.DeleteWorkflowsAsync(ids, request.DeleteChildren, ct).ConfigureAwait(false);
            return new SuccessResponse(request, success: true);
        }
        catch (Exception ex)
        {
            return new SuccessResponse(request, ex);
        }
    }

    private async Task<BaseResponse> HandleRecoveryAsync(RecoveryRequest request, CancellationToken ct)
    {
        try
        {
            var executorIds = request.ExecutorIds ?? new List<string>();
            await _executor.RecoverPendingWorkflowsAsync(executorIds, ct).ConfigureAwait(false);
            return new SuccessResponse(request, success: true);
        }
        catch (Exception ex)
        {
            return new SuccessResponse(request, ex);
        }
    }

    private async Task<BaseResponse> HandleListWorkflowsAsync(
        ListWorkflowsRequest request, CancellationToken ct)
    {
        try
        {
            var statuses = await _db.ListWorkflowsAsync(request.AsInput(), ct).ConfigureAwait(false);
            var output = statuses.Select(s => new WorkflowsOutput(s)).ToList();
            return new WorkflowOutputsResponse(request, output);
        }
        catch (Exception ex)
        {
            return new WorkflowOutputsResponse(request, ex);
        }
    }

    private async Task<BaseResponse> HandleListQueuedWorkflowsAsync(
        ListQueuedWorkflowsRequest request, CancellationToken ct)
    {
        try
        {
            var statuses = await _db.ListWorkflowsAsync(request.AsInput(), ct).ConfigureAwait(false);
            var output = statuses.Select(s => new WorkflowsOutput(s)).ToList();
            return new WorkflowOutputsResponse(request, output);
        }
        catch (Exception ex)
        {
            return new WorkflowOutputsResponse(request, ex);
        }
    }

    private async Task<BaseResponse> HandleListStepsAsync(
        ListStepsRequest request, CancellationToken ct)
    {
        try
        {
            var steps = await _db.ListWorkflowStepsAsync(
                request.WorkflowId ?? string.Empty,
                request.LoadOutput,
                request.Limit,
                request.Offset,
                ct).ConfigureAwait(false);
            return new ListStepsResponse(request, steps.Select(s => new ListStepsResponse.StepEntry(s)).ToList());
        }
        catch (Exception ex)
        {
            return new ListStepsResponse(request, ex);
        }
    }

    private async Task<BaseResponse> HandleGetWorkflowAsync(
        GetWorkflowRequest request, CancellationToken ct)
    {
        try
        {
            var input = new ListWorkflowsInput(request.WorkflowId ?? string.Empty);
            var statuses = await _db.ListWorkflowsAsync(input, ct).ConfigureAwait(false);
            var output = statuses.Count == 0 ? null : new WorkflowsOutput(statuses[0]);
            return new GetWorkflowResponse(request, output);
        }
        catch (Exception ex)
        {
            return new GetWorkflowResponse(request, ex);
        }
    }

    private async Task<BaseResponse> HandleExistPendingWorkflowsAsync(
        ExistPendingWorkflowsRequest request, CancellationToken ct)
    {
        try
        {
            var executorId = request.ExecutorId ?? string.Empty;
            var pending = await _db.GetPendingWorkflowsAsync(
                [executorId], request.ApplicationVersion, ct).ConfigureAwait(false);
            return new ExistPendingWorkflowsResponse(request, pending.Count > 0);
        }
        catch (Exception ex)
        {
            return new ExistPendingWorkflowsResponse(request, ex);
        }
    }

    private async Task<BaseResponse> HandleListSchedulesAsync(
        ListSchedulesRequest request, CancellationToken ct)
    {
        try
        {
            var schedules = await _db.ListSchedulesAsync(
                request.ParsedStatuses(),
                request.WorkflowNames(),
                request.ScheduleNamePrefixes(),
                ct).ConfigureAwait(false);
            var loadCtx = request.ShouldLoadContext();
            var output = schedules.Select(s => ScheduleOutput.From(s, loadCtx)).ToList();
            return new ListSchedulesResponse(request, output);
        }
        catch (Exception ex)
        {
            return new ListSchedulesResponse(request, ex.Message);
        }
    }

    private async Task<BaseResponse> HandleGetScheduleAsync(
        GetScheduleRequest request, CancellationToken ct)
    {
        try
        {
            var schedule = await _db.GetScheduleAsync(request.ScheduleName ?? string.Empty, ct)
                .ConfigureAwait(false);
            var output = schedule is null ? null : ScheduleOutput.From(schedule, request.ShouldLoadContext);
            return new GetScheduleResponse(request, output);
        }
        catch (Exception ex)
        {
            return new GetScheduleResponse(request, ex.Message);
        }
    }

    private async Task<BaseResponse> HandlePauseScheduleAsync(
        PauseScheduleRequest request, CancellationToken ct)
    {
        try
        {
            await _db.PauseScheduleAsync(request.ScheduleName ?? string.Empty, ct).ConfigureAwait(false);
            return new SuccessResponse(request, success: true);
        }
        catch (Exception ex)
        {
            return new SuccessResponse(request, ex);
        }
    }

    private async Task<BaseResponse> HandleResumeScheduleAsync(
        ResumeScheduleRequest request, CancellationToken ct)
    {
        try
        {
            await _db.ResumeScheduleAsync(request.ScheduleName ?? string.Empty, ct).ConfigureAwait(false);
            return new SuccessResponse(request, success: true);
        }
        catch (Exception ex)
        {
            return new SuccessResponse(request, ex);
        }
    }

    private static IReadOnlyList<string> ResolveIds(IList<string>? plural, string? singular)
    {
        if (plural is { Count: > 0 }) return plural.ToList();
        return singular is null ? Array.Empty<string>() : [singular];
    }
}
