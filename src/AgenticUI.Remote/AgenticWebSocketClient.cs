using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AgenticUI;

namespace AgenticUI.Remote;

/// <summary>
/// Connects to the embedded gateway over WSS/TLS. Each message is one complete JSON object.
/// </summary>
public sealed class AgenticWebSocketClient : IAgenticRemoteClient
{
    private readonly WebSocket _socket;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<RemoteResponse>> _pending = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly AsyncLocal<bool> _insideTransportCallback = new();
    private readonly Task _readLoop;
    private bool _disposed;

    private AgenticWebSocketClient(WebSocket socket)
    {
        _socket = socket;
        _readLoop = ReadLoopAsync(_lifetime.Token);
    }

    public event Action<AgenticEvent>? EventReceived;

    public event Action<Exception>? ConnectionFaulted;

    public bool IsConnected => !_disposed && _socket.State == WebSocketState.Open;

    /// <summary>
    /// 首次连接先读取候选指纹，由用户在独立可信渠道核验后输入目标端的一次性码。
    /// 后续连接严格匹配已保存的证书，不自动覆盖身份、不自动重试配对。
    /// </summary>
    public static async Task<AgenticWebSocketClient> ConnectPairedAsync(Uri endpoint,
        Func<AgenticPairingPrompt, Task<string?>> confirmPairing, AgenticPairingStore? store = null,
        CancellationToken cancellationToken = default)
    {
        TlsWebSocketTransport.ValidateUri(endpoint);
        store ??= new AgenticPairingStore();
        var known = store.Load(endpoint);
        var fingerprint = known?.Fingerprint;
        var token = known?.Token;
        if (known is null)
        {
            using (var probeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                probeTimeout.CancelAfter(TimeSpan.FromSeconds(10));
                fingerprint = await TlsWebSocketTransport.ProbeAsync(endpoint, probeTimeout.Token).ConfigureAwait(false);
            }
            var code = await confirmPairing(new AgenticPairingPrompt(endpoint, fingerprint!)).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(code)) throw new OperationCanceledException("用户取消配对。");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            using var socket = await TlsWebSocketTransport.ConnectAsync(endpoint, fingerprint!, timeout.Token).ConfigureAwait(false);
            using var pairing = new AgenticWebSocketClient(socket);
            var response = await pairing.SendAsync(new RemoteRequest
                { Type = RemoteMessageTypes.Pair, AuthenticationToken = code!.Trim().ToUpperInvariant() }, timeout.Token).ConfigureAwait(false);
            if (response.Type != RemoteMessageTypes.Paired || string.IsNullOrWhiteSpace(response.PairingToken))
                throw new UnauthorizedAccessException(response.Error ?? "配对失败。");
            token = response.PairingToken;
        }
        using var authenticationTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        authenticationTimeout.CancelAfter(TimeSpan.FromSeconds(10));
        WebSocket connection;
        try
        {
            connection = await TlsWebSocketTransport.ConnectAsync(endpoint, fingerprint!, authenticationTimeout.Token).ConfigureAwait(false);
        }
        catch (System.Security.Authentication.AuthenticationException exception)
        {
            throw new System.Security.Authentication.AuthenticationException("目标证书不匹配、失效或 TLS 验证失败；已拒绝发送凭据，请核验目标身份。", exception);
        }
        var client = new AgenticWebSocketClient(connection);
        try
        {
            var response = await client.SendAsync(new RemoteRequest
                { Type = RemoteMessageTypes.Authenticate, AuthenticationToken = token }, authenticationTimeout.Token).ConfigureAwait(false);
            if (response.Type != RemoteMessageTypes.Authenticated) throw new UnauthorizedAccessException("配对凭据已失效。");
            if (known is null) store.Save(endpoint, fingerprint!, token!);
            return client;
        }
        catch { client.Dispose(); throw; }
    }

    public static async Task<AgenticWebSocketClient> ConnectAsync(
        Uri webSocketUri,
        string authenticationToken,
        string? clientName = null,
        bool skipTlsValidationForDevelopment = false,
        CancellationToken cancellationToken = default)
    {
        if (webSocketUri.Scheme != "wss")
        {
            throw new ArgumentException("Only wss:// endpoints are supported.", nameof(webSocketUri));
        }

        if (string.IsNullOrWhiteSpace(authenticationToken))
        {
            throw new ArgumentException("An authentication token is required.", nameof(authenticationToken));
        }

        var socket = new ClientWebSocket();
        if (skipTlsValidationForDevelopment)
        {
            if (!webSocketUri.IsLoopback)
            {
                socket.Dispose();
                throw new ArgumentException("开发模式跳过验证仅限回环测试；网络连接请使用配对或可信证书。");
            }
#if NET5_0_OR_GREATER
            socket.Options.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
#endif
        }

        try
        {
            await socket.ConnectAsync(webSocketUri, cancellationToken).ConfigureAwait(false);
            var client = new AgenticWebSocketClient(socket);
            var authentication = await client.SendAsync(
                new RemoteRequest
                {
                    Type = RemoteMessageTypes.Authenticate,
                    AuthenticationToken = authenticationToken,
                    ClientName = clientName
                },
                cancellationToken).ConfigureAwait(false);
            if (authentication.Type != RemoteMessageTypes.Authenticated)
            {
                client.Dispose();
                throw new UnauthorizedAccessException(authentication.Error ?? "AgenticUI authentication failed.");
            }

            return client;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public Task<RemoteResponse> ListControlsAsync(CancellationToken cancellationToken = default) =>
        ListControlsAsync(includeHidden: false, cancellationToken);

    public Task<RemoteResponse> ListControlsAsync(
        bool includeHidden,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            new RemoteRequest
            {
                Type = RemoteMessageTypes.ListControls,
                IncludeHidden = includeHidden
            },
            cancellationToken);

    public Task<RemoteResponse> ExecuteAsync(
        AgenticCommand command,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            new RemoteRequest { Type = RemoteMessageTypes.Execute, Command = command },
            cancellationToken);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetime.Cancel();
        // Dispose 必须立即终止传输，不能在 UI 线程无限等待对端关闭握手。
        _socket.Abort();
        _socket.Dispose();
        FailPending(new ObjectDisposedException(nameof(AgenticWebSocketClient)));
        _ = DisposeAfterReadLoopAsync();
    }

    private async Task DisposeAfterReadLoopAsync()
    {
        try { await _readLoop.ConfigureAwait(false); }
        catch (Exception exception) when (exception is OperationCanceledException or WebSocketException or ObjectDisposedException) { }
        // 发送调用可能正在 finally 中释放写锁，因此不抢先销毁其 SemaphoreSlim。
        _lifetime.Dispose();
    }

    private async Task<RemoteResponse> SendAsync(
        RemoteRequest request,
        CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AgenticWebSocketClient));
        }

        var completion = new TaskCompletionSource<RemoteResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(request.RequestId, completion))
        {
            throw new InvalidOperationException($"Duplicate request ID '{request.RequestId}'.");
        }

        using var registration = cancellationToken.Register(
            () =>
            {
                if (_pending.TryRemove(request.RequestId, out var pending))
                {
                    pending.TrySetCanceled();
                }
            });

        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(request, AgenticJson.Options);
            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
#if NET8_0_OR_GREATER
                await _socket.SendAsync(
                    payload,
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken).ConfigureAwait(false);
#else
                await _socket.SendAsync(
                    new ArraySegment<byte>(payload),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken).ConfigureAwait(false);
#endif
            }
            finally
            {
                _writeLock.Release();
            }

            return await completion.Task.ConfigureAwait(false);
        }
        catch
        {
            _pending.TryRemove(request.RequestId, out _);
            throw;
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _socket.State == WebSocketState.Open)
            {
                var response = await ReceiveResponseAsync(cancellationToken).ConfigureAwait(false);
                if (response is null)
                {
                    throw new EndOfStreamException("The AgenticUI WebSocket was closed.");
                }

                if (response.Type == RemoteMessageTypes.Event && response.Event is not null)
                {
                    RaiseEvent(response.Event);
                    continue;
                }

                if (response.RequestId is not null &&
                    _pending.TryRemove(response.RequestId, out var completion))
                {
                    completion.TrySetResult(response);
                }
            }
        }
        catch (Exception exception) when (
            exception is WebSocketException or ObjectDisposedException or IOException or JsonException or OperationCanceledException)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                FailPending(exception);
                RaiseConnectionFaulted(exception);
            }
        }
    }

    private async Task<RemoteResponse?> ReceiveResponseAsync(CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        var buffer = new byte[8192];

        while (true)
        {
#if NET8_0_OR_GREATER
            var result = await _socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
#else
            var result = await _socket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                cancellationToken).ConfigureAwait(false);
#endif
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                throw new InvalidDataException("Only JSON text messages are supported.");
            }
            if (stream.Length + result.Count > 1024 * 1024)
                throw new InvalidDataException("WebSocket response exceeds 1 MiB.");

            var segment = new ArraySegment<byte>(buffer, 0, result.Count);
#if NET8_0_OR_GREATER
            await stream.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken).ConfigureAwait(false);
#else
            await stream.WriteAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken)
                .ConfigureAwait(false);
#endif
            if (result.EndOfMessage)
            {
                break;
            }
        }

        stream.Position = 0;
        return await JsonSerializer.DeserializeAsync<RemoteResponse>(
            stream,
            AgenticJson.Options,
            cancellationToken).ConfigureAwait(false);
    }

    private void RaiseEvent(AgenticEvent message)
    {
        var handlers = EventReceived;
        if (handlers is null)
        {
            return;
        }

        foreach (Action<AgenticEvent> handler in handlers.GetInvocationList())
        {
            var wasInsideCallback = _insideTransportCallback.Value;
            _insideTransportCallback.Value = true;
            try
            {
                handler(message);
            }
            catch
            {
            }
            finally
            {
                _insideTransportCallback.Value = wasInsideCallback;
            }
        }
    }

    private void RaiseConnectionFaulted(Exception exception)
    {
        var handlers = ConnectionFaulted;
        if (handlers is null)
        {
            return;
        }

        foreach (Action<Exception> handler in handlers.GetInvocationList())
        {
            var wasInsideCallback = _insideTransportCallback.Value;
            _insideTransportCallback.Value = true;
            try
            {
                handler(exception);
            }
            catch
            {
            }
            finally
            {
                _insideTransportCallback.Value = wasInsideCallback;
            }
        }
    }

    private void FailPending(Exception exception)
    {
        foreach (var pair in _pending.ToArray())
        {
            if (_pending.TryRemove(pair.Key, out var completion))
            {
                completion.TrySetException(exception);
            }
        }
    }
}
