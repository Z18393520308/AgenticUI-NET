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
        finally
        {
            _clients.TryRemove(id, out _);
            connection.Dispose();
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
                    Controls = _registry.Snapshot()
                };
            case RemoteMessageTypes.Execute when request.Command is not null:
                return new RemoteResponse
                {
                    RequestId = request.RequestId,
                    Type = RemoteMessageTypes.Result,
                    Result = await _dispatcher.DispatchAsync(request.Command, cancellationToken).ConfigureAwait(false)
                };
            default:
                return Error(request.RequestId, $"Unsupported request type '{request.Type}'.");
        }
    }

    private async ValueTask BroadcastEventAsync(AgenticEvent message)
    {
        var response = new RemoteResponse { Type = RemoteMessageTypes.Event, Event = message };
        foreach (var pair in _clients.ToArray())
        {
            if (_options.RequireAuthentication && !pair.Value.IsAuthenticated)
            {
                continue;
            }
            try
            {
                await pair.Value.SendAsync(response, CancellationToken.None).ConfigureAwait(false);
            }
            catch (IOException)
            {
                if (_clients.TryRemove(pair.Key, out var connection))
                {
                    connection.Dispose();
                }
            }
        }
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

        public ClientConnection(NamedPipeServerStream pipe)
        {
            Pipe = pipe;
            Reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, true);
            Writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
        }

        public NamedPipeServerStream Pipe { get; }
        public StreamReader Reader { get; }
        public StreamWriter Writer { get; }
        public bool IsAuthenticated { get; set; }
        public string? ClientName { get; set; }

        public async Task SendAsync(RemoteResponse response, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(response, AgenticJson.Options);
            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await Writer.WriteLineAsync(json).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public void Dispose()
        {
            Reader.Dispose();
            Writer.Dispose();
            Pipe.Dispose();
            _writeLock.Dispose();
        }
    }
}
