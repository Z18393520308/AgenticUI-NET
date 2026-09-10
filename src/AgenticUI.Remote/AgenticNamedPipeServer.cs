using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AgenticUI;

namespace AgenticUI.Remote;

public sealed class AgenticNamedPipeServer : IDisposable
{
    private readonly string _pipeName;
    private readonly AgenticControlRegistry _registry;
    private readonly AgenticCommandDispatcher _dispatcher;
    private readonly AgenticNamedPipeServerOptions _options;
    private readonly ConcurrentDictionary<int, ClientConnection> _clients = new();
    private readonly IDisposable _eventSubscription;
    private CancellationTokenSource? _lifetime;
    private Task? _acceptLoop;
    private NamedPipeServerStream? _pendingServer;
    private int _clientId;

    public AgenticNamedPipeServer(
        string pipeName = "AgenticUI.NET",
        AgenticControlRegistry? registry = null,
        AgenticCommandDispatcher? dispatcher = null,
        AgenticEventBus? events = null,
        AgenticNamedPipeServerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(pipeName))
        {
            throw new ArgumentException("A pipe name is required.", nameof(pipeName));
        }

        _pipeName = pipeName;
        _options = options ?? new AgenticNamedPipeServerOptions();
        AuthenticationToken = _options.ResolveAuthenticationToken();
        if (_options.MaximumMessageLength < 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MaximumMessageLength must be at least 1024 bytes.");
        }
        _registry = registry ?? AgenticControlRegistry.Default;
        _dispatcher = dispatcher ?? new AgenticCommandDispatcher(_registry, events);
        _eventSubscription = (events ?? AgenticEventBus.Default).Subscribe(BroadcastEventAsync);
    }

    public bool IsRunning => _lifetime is not null;
    public string AuthenticationToken { get; }

    public void Start()
    {
        if (_lifetime is not null)
        {
            return;
        }

        _lifetime = new CancellationTokenSource();
        _acceptLoop = AcceptLoopAsync(_lifetime.Token);
    }

    public async Task StopAsync()
    {
        var lifetime = _lifetime;
        if (lifetime is null)
        {
            return;
        }

        _lifetime = null;
        lifetime.Cancel();
        Interlocked.Exchange(ref _pendingServer, null)?.Dispose();
        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }

        if (_acceptLoop is not null)
        {
            try
            {
                await _acceptLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        lifetime.Dispose();
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _eventSubscription.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                CreatePipeOptions());
            Interlocked.Exchange(ref _pendingServer, pipe)?.Dispose();
            try
            {
#if NET8_0_OR_GREATER
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
#else
                await Task.Run(() => pipe.WaitForConnection(), cancellationToken).ConfigureAwait(false);
#endif
                Interlocked.CompareExchange(ref _pendingServer, null, pipe);
            }
            catch
            {
                Interlocked.CompareExchange(ref _pendingServer, null, pipe);
                pipe.Dispose();
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                throw;
            }

            var id = Interlocked.Increment(ref _clientId);
            var connection = new ClientConnection(pipe);
            _clients[id] = connection;
            _ = HandleClientAsync(id, connection, cancellationToken);
        }
    }

    private async Task HandleClientAsync(
        int id,
        ClientConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && connection.Pipe.IsConnected)
            {
                var line = await connection.Reader.ReadLineAsync().ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }
                if (line.Length > _options.MaximumMessageLength)
                {
                    await connection.SendAsync(
                        Error(null, "Message exceeds the configured size limit."),
                        cancellationToken).ConfigureAwait(false);
                    break;
                }

                RemoteResponse response;
                RemoteRequest? request = null;
                try
                {
                    request = JsonSerializer.Deserialize<RemoteRequest>(line, AgenticJson.Options);
                    response = request is null
                        ? Error(null, "Empty request.")
                        : await ProcessAsync(connection, request, cancellationToken).ConfigureAwait(false);
                }
                catch (JsonException exception)
                {
                    response = Error(request?.RequestId, $"Invalid JSON: {exception.Message}");
                }
                catch (Exception exception)
                {
                    response = Error(request?.RequestId, exception.Message);
                }

                await connection.SendAsync(response, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _clients.TryRemove(id, out _);
            connection.Dispose();
            await _registry.ClearGuidanceAsync(connection.SessionId).ConfigureAwait(false);
        }
    }

    private async Task<RemoteResponse> ProcessAsync(
        ClientConnection connection,
        RemoteRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Type == RemoteMessageTypes.Authenticate)
        {
            if (!_options.RequireAuthentication ||
                AgenticRemoteSecurity.FixedTimeEquals(AuthenticationToken, request.AuthenticationToken))
            {
                connection.IsAuthenticated = true;
                connection.ClientName = request.ClientName;
                return new RemoteResponse
                {
                    RequestId = request.RequestId,
                    Type = RemoteMessageTypes.Authenticated
                };
            }

            return Error(request.RequestId, "Authentication failed.");
        }

        if (_options.RequireAuthentication && !connection.IsAuthenticated)
        {
            return Error(request.RequestId, "Authenticate before sending commands.");
        }

        switch (request.Type)
        {
            case RemoteMessageTypes.ListControls:
                return new RemoteResponse
                {
                    RequestId = request.RequestId,
                    Type = RemoteMessageTypes.Controls,
                    Controls = _registry.Snapshot(remotelyDiscoverableOnly: !request.IncludeHidden)
                        .Select(AgenticPrivacy.SanitizeDescriptor).ToArray()
                };
            case RemoteMessageTypes.Execute when request.Command is not null:
                request.Command.SessionId = connection.SessionId;
                var result = await _dispatcher.DispatchAsync(request.Command, cancellationToken).ConfigureAwait(false);
                if (result.Control is not null) result.Control = AgenticPrivacy.SanitizeDescriptor(result.Control);
                return new RemoteResponse
                {
                    RequestId = request.RequestId,
                    Type = RemoteMessageTypes.Result,
                    Result = result
                };
            default:
                return Error(request.RequestId, $"Unsupported request type '{request.Type}'.");
        }
    }

    private ValueTask BroadcastEventAsync(AgenticEvent message)
    {
        var sensitive = message.IsSensitive;
        if (!sensitive && _registry.TryGet(message.ControlId, out var control))
        {
            try { sensitive = control?.Describe().IsSensitive == true; }
            catch { sensitive = true; }
        }
        var response = new RemoteResponse { Type = RemoteMessageTypes.Event,
            Event = AgenticPrivacy.SanitizeEvent(message, sensitive) };
        foreach (var pair in _clients.ToArray())
        {
            if (_options.RequireAuthentication && !pair.Value.IsAuthenticated) continue;
            // 有界队列：慢客户端断开，不阻塞 UI 事件和业务动作，也不悄悄丢失审计事件。
            if (!pair.Value.TryQueueEvent(response) && _clients.TryRemove(pair.Key, out var connection))
                connection.Dispose();
        }
        return default;
    }

    private static RemoteResponse Error(string? requestId, string message) =>
        new() { RequestId = requestId, Type = RemoteMessageTypes.Error, Error = message };

    private static PipeOptions CreatePipeOptions()
    {
#if NET8_0_OR_GREATER
        return PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly;
#else
        return PipeOptions.Asynchronous;
#endif
    }

    private sealed class ClientConnection : IDisposable
    {
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly SemaphoreSlim _eventSignal = new(0);
        private readonly ConcurrentQueue<RemoteResponse> _events = new();
        private readonly CancellationTokenSource _closed = new();
        private int _eventCount;
        private int _disposed;
        public string SessionId { get; } = Guid.NewGuid().ToString("N");

        public ClientConnection(NamedPipeServerStream pipe)
        {
            Pipe = pipe;
            Reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, true);
            Writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
            _ = PumpEventsAsync();
        }

        public NamedPipeServerStream Pipe { get; }
        public StreamReader Reader { get; }
        public StreamWriter Writer { get; }
        public bool IsAuthenticated { get; set; }
        public string? ClientName { get; set; }

        public bool TryQueueEvent(RemoteResponse response)
        {
            if (Volatile.Read(ref _disposed) != 0) return false;
            if (Interlocked.Increment(ref _eventCount) > 256)
            {
                Interlocked.Decrement(ref _eventCount);
                return false;
            }
            _events.Enqueue(response);
            _eventSignal.Release();
            return true;
        }

        private async Task PumpEventsAsync()
        {
            try
            {
                while (!_closed.IsCancellationRequested)
                {
                    await _eventSignal.WaitAsync(_closed.Token).ConfigureAwait(false);
                    if (_events.TryDequeue(out var response))
                    {
                        Interlocked.Decrement(ref _eventCount);
                        await SendAsync(response, _closed.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException or OperationCanceledException)
            { Dispose(); }
        }

        public async Task SendAsync(RemoteResponse response, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(response, AgenticJson.Options);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _closed.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            // net48 StreamWriter 没有带 CancellationToken 的 WriteLineAsync，用关闭管道解除慢写。
            using var registration = timeout.Token.Register(() => Pipe.Dispose());
            await _writeLock.WaitAsync(timeout.Token).ConfigureAwait(false);
            try { await Writer.WriteLineAsync(json).ConfigureAwait(false); }
            finally { _writeLock.Release(); }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _closed.Cancel();
            Pipe.Dispose();
            // 不在 UI/事件线程等待写锁。流先断开，读写任务自行退出。
            _ = DisposeStreamsAsync();
        }

        private async Task DisposeStreamsAsync()
        {
            await _writeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                try { Reader.Dispose(); Writer.Dispose(); }
                catch (Exception exception) when (exception is IOException or ObjectDisposedException) { }
            }
            finally { _writeLock.Release(); }
        }
    }
}
