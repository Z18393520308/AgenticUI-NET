using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AgenticUI;

namespace AgenticUI.Remote;

public sealed class AgenticNamedPipeClient : IDisposable
{
    private readonly NamedPipeClientStream _pipe;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<RemoteResponse>> _pending = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly AsyncLocal<bool> _insideTransportCallback = new();
    private readonly Task _readLoop;
    private bool _disposed;

    private AgenticNamedPipeClient(NamedPipeClientStream pipe)
    {
        _pipe = pipe;
        _reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, true);
        _writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
        _readLoop = ReadLoopAsync(_lifetime.Token);
    }

    public event Action<AgenticEvent>? EventReceived;
    public event Action<Exception>? ConnectionFaulted;

    public bool IsConnected => !_disposed && _pipe.IsConnected;

    public static async Task<AgenticNamedPipeClient> ConnectAsync(
        string authenticationToken,
        string pipeName = "AgenticUI.NET",
        string? clientName = null,
        int timeoutMilliseconds = 5000,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authenticationToken))
        {
            throw new ArgumentException("An authentication token is required.", nameof(authenticationToken));
        }

        var pipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        try
        {
#if NET8_0_OR_GREATER
            await pipe.ConnectAsync(timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
#else
            await Task.Run(() => pipe.Connect(timeoutMilliseconds), cancellationToken).ConfigureAwait(false);
#endif
            var client = new AgenticNamedPipeClient(pipe);
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
            pipe.Dispose();
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
        _writer.Dispose();
        _pipe.Dispose();
        FailPending(new ObjectDisposedException(nameof(AgenticNamedPipeClient)));
        if (!_insideTransportCallback.Value)
        {
            try
            {
                _readLoop.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        _reader.Dispose();
        _writeLock.Dispose();
        _lifetime.Dispose();
    }

    private async Task<RemoteResponse> SendAsync(
        RemoteRequest request,
        CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AgenticNamedPipeClient));
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
            var json = JsonSerializer.Serialize(request, AgenticJson.Options);
            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _writer.WriteLineAsync(json).ConfigureAwait(false);
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
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await _reader.ReadLineAsync().ConfigureAwait(false);
                if (line is null)
                {
                    throw new EndOfStreamException("The AgenticUI pipe was closed.");
                }

                var response = JsonSerializer.Deserialize<RemoteResponse>(line, AgenticJson.Options)
                               ?? throw new InvalidDataException("The AgenticUI response was empty.");
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
            exception is IOException or ObjectDisposedException or EndOfStreamException or JsonException)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                FailPending(exception);
                RaiseConnectionFaulted(exception);
            }
        }
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
                // A client observer must not terminate the transport receive loop.
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
