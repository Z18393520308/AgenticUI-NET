using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;

namespace AgenticUI.Remote;

/// <summary>Windows HTTP.sys 进程内监听；TLS 由系统证书绑定处理，不实现自定义 HTTP/TLS 协议。</summary>
internal sealed class EmbeddedGateway : IDisposable
{
    private readonly AgenticHostOptions _options;
    private readonly string _pipeToken;
    private readonly string _networkToken;
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly ConcurrentDictionary<int, WebSocket> _sockets = new();
    private readonly Dictionary<string, RequestWindow> _attempts = new(StringComparer.Ordinal);
    private readonly object _attemptsGate = new();
    private readonly string _instanceId = Guid.NewGuid().ToString("N");
    private UdpClient? _udp;
    private int _connections;
    private int _nextId;
    private int _disposed;
    private volatile bool _running;
    private volatile bool _discoveryRunning;

    public EmbeddedGateway(AgenticHostOptions options, string pipeToken, string networkToken)
    { _options = options; _pipeToken = pipeToken; _networkToken = networkToken; }
    public bool IsRunning => _running;
    public bool DiscoveryRunning => _discoveryRunning;

    public void Start()
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            throw new PlatformNotSupportedException("进程内 HTTPS 服务需要 Windows HTTP.sys。");
        _listener.Prefixes.Add(_options.Network.ListenUrl.TrimEnd('/') + "/");
        _listener.Start();
        _running = true;
        _ = Task.Run(AcceptAsync);
        if (_options.Discovery.Enabled)
        {
            _udp = new UdpClient { EnableBroadcast = true };
            _ = Task.Run(BroadcastAsync);
        }
    }

    private async Task AcceptAsync()
    {
        try
        {
            while (!_lifetime.IsCancellationRequested)
            {
                var context = await _listener.GetContextAsync().ConfigureAwait(false);
                if (!context.Request.IsSecureConnection ||
                    context.Request.Url?.AbsolutePath != _options.Network.WebSocketPath ||
                    !context.Request.IsWebSocketRequest)
                { Reject(context, 400); continue; }
                // 有 Origin 的请求必须显式匹配；没有配置 Origin 时仅允许非浏览器客户端。
                var origin = context.Request.Headers["Origin"];
                if (origin is not null && !_options.Network.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
                { Reject(context, 403); continue; }
                var address = context.Request.RemoteEndPoint?.Address.ToString() ?? "unknown";
                if (!AllowConnection(address)) { Reject(context, 429); continue; }
                if (Interlocked.Increment(ref _connections) > _options.Network.MaxConnections)
                { Interlocked.Decrement(ref _connections); Reject(context, 503); continue; }
                _ = HandleAsync(context);
            }
        }
        catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException or InvalidOperationException)
        {
            if (!_lifetime.IsCancellationRequested) Trace.TraceError("AgenticUI 网络监听中断，请重启应用并检查 HTTP.sys 配置。");
        }
        finally { _running = false; _discoveryRunning = false; }
    }

    private bool AllowConnection(string address)
    {
        lock (_attemptsGate)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var key in _attempts.Where(pair => now - pair.Value.Started >= TimeSpan.FromMinutes(1)).Select(pair => pair.Key).ToArray())
                _attempts.Remove(key);
            if (!_attempts.TryGetValue(address, out var window))
            {
                // 来源表也有界，拒绝新来源而不是无限保存攻击者伪造的来源记录。
                if (_attempts.Count >= 1024) return false;
                _attempts[address] = window = new RequestWindow(_options.Network.ConnectionAttemptsPerMinute);
            }
            return window.TryAcquire();
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        var id = Interlocked.Increment(ref _nextId);
        WebSocket? socket = null;
        try
        {
            var accepted = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
            socket = accepted.WebSocket;
            _sockets[id] = socket;
            using var registration = _lifetime.Token.Register(socket.Abort);
            await EmbeddedGatewaySession.RunAsync(socket, _options, _pipeToken, _networkToken, _lifetime.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpListenerException or WebSocketException or IOException or
            OperationCanceledException or ObjectDisposedException or InvalidOperationException)
        { if (!_lifetime.IsCancellationRequested) Trace.TraceWarning("AgenticUI 网络会话结束；未记录请求参数或令牌。"); }
        finally
        {
            _sockets.TryRemove(id, out _);
            socket?.Dispose();
            try { context.Response.Close(); }
            catch (Exception exception) when (exception is ObjectDisposedException or HttpListenerException) { }
            Interlocked.Decrement(ref _connections);
        }
    }

    private static void Reject(HttpListenerContext context, int status)
    { context.Response.StatusCode = status; context.Response.Close(); }

    internal byte[] CreateAnnouncement() => JsonSerializer.SerializeToUtf8Bytes(new AgenticGatewayDiscoveryAnnouncement
    {
        InstanceId = _instanceId,
        ServiceName = _options.Discovery.ServiceName,
        WebSocketUrl = _options.Discovery.PublicWebSocketUrl,
        Version = typeof(AgenticApplicationHost).Assembly.GetName().Version?.ToString() ?? "unknown",
        Timestamp = DateTimeOffset.UtcNow
    }, AgenticJson.Options);

    private async Task BroadcastAsync()
    {
        try
        {
            while (!_lifetime.IsCancellationRequested && IsRunning)
            {
                var bytes = CreateAnnouncement();
                await _udp!.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, _options.Discovery.Port)).ConfigureAwait(false);
                await _udp.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Loopback, _options.Discovery.Port)).ConfigureAwait(false);
                _discoveryRunning = true;
                await Task.Delay(TimeSpan.FromSeconds(_options.Discovery.IntervalSeconds), _lifetime.Token).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException or OperationCanceledException)
        { if (!_lifetime.IsCancellationRequested) Trace.TraceWarning("AgenticUI UDP 发现发送失败，网络控制不受影响。"); }
        finally { _discoveryRunning = false; }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _running = false;
        _discoveryRunning = false;
        _lifetime.Cancel();
        _listener.Close();
        _udp?.Dispose();
        foreach (var socket in _sockets.Values) socket.Abort();
        // 后台会话仍持有取消令牌；不在 UI 退出回调中同步等待或提前释放 CTS。
    }

    internal sealed class RequestWindow
    {
        private readonly int _limit;
        private int _count;
        public DateTimeOffset Started { get; private set; } = DateTimeOffset.UtcNow;
        public RequestWindow(int limit) => _limit = limit;
        public bool TryAcquire()
        {
            if (DateTimeOffset.UtcNow - Started >= TimeSpan.FromMinutes(1)) { Started = DateTimeOffset.UtcNow; _count = 0; }
            return ++_count <= _limit;
        }
    }
}
