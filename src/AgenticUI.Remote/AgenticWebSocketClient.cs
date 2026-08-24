using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AgenticUI;

namespace AgenticUI.Remote;

/// <summary>
/// Connects to <see cref="AgenticUI.Gateway"/> over WSS/TLS. Each message is one complete JSON object.
/// </summary>
public sealed class AgenticWebSocketClient : IAgenticRemoteClient
{
    private readonly ClientWebSocket _socket;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<RemoteResponse>> _pending = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly AsyncLocal<bool> _insideTransportCallback = new();
    private readonly Task _readLoop;
    private bool _disposed;

    private AgenticWebSocketClient(ClientWebSocket socket)
    {
        _socket = socket;
        _readLoop = ReadLoopAsync(_lifetime.Token);
    }

    public event Action<AgenticEvent>? EventReceived;

    public event Action<Exception>? ConnectionFaulted;

    public bool IsConnected => !_disposed && _socket.State == WebSocketState.Open;

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
        try
        {
            if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
        }
        catch (WebSocketException)
        {
        }

        _socket.Dispose();
        FailPending(new ObjectDisposedException(nameof(AgenticWebSocketClient)));
        if (!_insideTransportCallback.Value)
        {
            try
            {
                _readLoop.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
            }
            catch (WebSocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        _writeLock.Dispose();
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
            exception is WebSocketException or ObjectDisposedException or EndOfStreamException or JsonException)
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
