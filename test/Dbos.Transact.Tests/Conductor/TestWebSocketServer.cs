using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

namespace Dbos.Transact.Tests.Conductor;

/// <summary>
/// In-memory WebSocket server for Conductor tests. Listens on a free loopback port,
/// accepts a single (or multiple) WebSocket connection(s), and exposes a queue of received
/// text messages plus a method for sending arbitrary text to the client.
/// </summary>
internal sealed class TestWebSocketServer : IAsyncDisposable
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _acceptLoop;
    private WebSocket? _activeSocket;
    private readonly TaskCompletionSource _connected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentQueue<string> _receivedMessages = new();
    private readonly SemaphoreSlim _receivedSignal = new(0);

    public Uri Url { get; }
    public Task ConnectedAsync => _connected.Task;
    public int ConnectionCount { get; private set; }

    public TestWebSocketServer(string path = "/")
    {
        var port = GetFreePort();
        // HttpListener prefixes must end with '/'. The match also covers the prefix-less URL.
        var prefixPath = path.EndsWith('/') ? path : path + "/";
        var prefix = $"http://127.0.0.1:{port}{prefixPath}";
        Url = new Uri($"ws://127.0.0.1:{port}{path.TrimEnd('/')}");

        _listener = new HttpListener();
        _listener.Prefixes.Add(prefix);
        _listener.Start();
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    private static int GetFreePort()
    {
        using var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch { return; }

            if (!ctx.Request.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.Close();
                continue;
            }

            var wsCtx = await ctx.AcceptWebSocketAsync(subProtocol: null).ConfigureAwait(false);
            _activeSocket = wsCtx.WebSocket;
            ConnectionCount++;
            _connected.TrySetResult();

            _ = Task.Run(() => ReceiveLoopAsync(wsCtx.WebSocket, ct), ct);
        }
    }

    private async Task ReceiveLoopAsync(WebSocket ws, CancellationToken ct)
    {
        var buf = new byte[8192];
        var sb = new StringBuilder();
        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await ws.ReceiveAsync(buf, ct).ConfigureAwait(false);
            }
            catch { return; }

            if (result.MessageType == WebSocketMessageType.Close) return;

            sb.Append(Encoding.UTF8.GetString(buf, 0, result.Count));
            if (result.EndOfMessage)
            {
                _receivedMessages.Enqueue(sb.ToString());
                sb.Clear();
                _receivedSignal.Release();
            }
        }
    }

    /// <summary>Sends a JSON message to the connected client.</summary>
    public async Task SendAsync(string text, CancellationToken ct = default)
    {
        var ws = _activeSocket ?? throw new InvalidOperationException("No client connected.");
        var bytes = Encoding.UTF8.GetBytes(text);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct).ConfigureAwait(false);
    }

    /// <summary>Closes the active connection (the client should reconnect).</summary>
    public async Task CloseConnectionAsync(WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure)
    {
        var ws = _activeSocket;
        if (ws is null) return;
        try { await ws.CloseAsync(status, "test", CancellationToken.None).ConfigureAwait(false); }
        catch { /* best-effort */ }
        _activeSocket = null;
    }

    /// <summary>Waits for the next message from the client (with timeout).</summary>
    public async Task<string> ReadMessageAsync(TimeSpan timeout)
    {
        if (!await _receivedSignal.WaitAsync(timeout).ConfigureAwait(false))
            throw new TimeoutException($"No message received within {timeout}.");
        return _receivedMessages.TryDequeue(out var msg)
            ? msg
            : throw new InvalidOperationException("Signal released but no message present.");
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        try { _listener.Stop(); } catch { /* best-effort */ }
        try { await _acceptLoop.ConfigureAwait(false); } catch { /* best-effort */ }
        _listener.Close();
        _cts.Dispose();
        _receivedSignal.Dispose();
    }
}
