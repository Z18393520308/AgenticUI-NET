using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Text.Json;

namespace AgenticUI.Remote;

/// <summary>用户态 TCP/TLS 网关；不注册 HTTP.sys、不修改系统证书库。</summary>
internal sealed class EmbeddedGateway : IDisposable
{
    private readonly AgenticHostOptions _options;
    private readonly string _pipeToken;
    private readonly string _networkToken;
    private TcpListener? _listener;
    private readonly AgenticPairingService? _pairing;
    private readonly ConcurrentDictionary<int, TcpClient> _clients = new();
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

    public EmbeddedGateway(AgenticHostOptions options, string pipeToken, string networkToken, AgenticPairingService? pairing = null)
    { _options = options; _pipeToken = pipeToken; _networkToken = networkToken; _pairing = pairing; }
    public bool IsRunning => _running;
    public bool DiscoveryRunning => _discoveryRunning;

    public void Start()
    {
        if (_pairing is null) throw new InvalidOperationException("缺少网关身份。");
        var uri = new Uri(_options.Network.ListenUrl);
        var address = IPAddress.Parse(_options.Network.BindAddress);
        _listener = new TcpListener(address, uri.Port);
        _listener.Server.ExclusiveAddressUse = true;
        _listener.Start(_options.Network.MaxConnections);
        _pairing.Revoked += AbortClients;
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
                var client = await _listener!.AcceptTcpClientAsync().ConfigureAwait(false);
                var address = (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "unknown";
                if (!AllowConnection(address)) { client.Dispose(); continue; }
                if (Interlocked.Increment(ref _connections) > _options.Network.MaxConnections)
                { Interlocked.Decrement(ref _connections); client.Dispose(); continue; }
                var id = Interlocked.Increment(ref _nextId);
                _clients[id] = client;
                _ = HandleAsync(client, id);
            }
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException or InvalidOperationException)
        {
            if (!_lifetime.IsCancellationRequested) Trace.TraceError("AgenticUI 网络监听中断，请检查监听地址及端口。");
        }
        finally { _running = false; _discoveryRunning = false; AbortClients(); }
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

    private async Task HandleAsync(TcpClient client, int id)
    {
        WebSocket? socket = null;
        try
        {
            client.NoDelay = true;
            using var connection = client;
            using var stream = new SslStream(client.GetStream(), false);
            using var stopRegistration = _lifetime.Token.Register(client.Close);
            using (var handshake = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token))
            {
                handshake.CancelAfter(TimeSpan.FromSeconds(10));
                using var abortHandshake = handshake.Token.Register(client.Close);
                await stream.AuthenticateAsServerAsync(_pairing!.Certificate, false, SslProtocols.Tls12, false).ConfigureAwait(false);
                socket = await TlsWebSocketTransport.AcceptAsync(stream, _options.Network.WebSocketPath,
                    _options.Network.AllowedOrigins, handshake.Token).ConfigureAwait(false);
            }
            _sockets[id] = socket;
            using var registration = _lifetime.Token.Register(socket.Abort);
            await EmbeddedGatewaySession.RunAsync(socket, _options, _pipeToken, _networkToken, _lifetime.Token, _pairing).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is SocketException or AuthenticationException or WebSocketException or IOException or
            OperationCanceledException or ObjectDisposedException or InvalidOperationException or FormatException or
            System.Security.Cryptography.CryptographicException or ArgumentException)
        { if (!_lifetime.IsCancellationRequested) Trace.TraceWarning("AgenticUI 网络会话结束；未记录请求参数或令牌。"); }
        finally
        {
            _sockets.TryRemove(id, out _);
            socket?.Dispose();
            _clients.TryRemove(id, out _);
            client.Dispose();
            Interlocked.Decrement(ref _connections);
        }
    }

    private void AbortClients()
    {
        foreach (var client in _clients.Values) client.Close();
        foreach (var socket in _sockets.Values) socket.Abort();
    }

    internal byte[] CreateAnnouncement() => JsonSerializer.SerializeToUtf8Bytes(new AgenticGatewayDiscoveryAnnouncement
    {
        InstanceId = _instanceId,
        ServiceName = _options.Discovery.ServiceName,
        WebSocketUrl = string.IsNullOrWhiteSpace(_options.Discovery.PublicWebSocketUrl)
            ? new UriBuilder(_options.Network.ListenUrl) { Scheme = "wss", Path = _options.Network.WebSocketPath }.Uri.AbsoluteUri
            : _options.Discovery.PublicWebSocketUrl,
        CertificateFingerprint = _pairing?.CertificateFingerprint ?? "",
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
        _listener?.Stop();
        if (_pairing is not null) _pairing.Revoked -= AbortClients;
        _udp?.Dispose();
        AbortClients();
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
