using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using AgenticUI.Remote;

namespace AgenticUI.Gateway;

internal sealed class GatewayConnectionHandler
{
    private const int MaximumRememberedRequestIds = 2048;
    private readonly GatewayOptions _options;
    private readonly GatewayActionPolicy _actionPolicy;
    private readonly ILogger<GatewayConnectionHandler> _logger;

    public GatewayConnectionHandler(
        GatewayOptions options,
        GatewayActionPolicy actionPolicy,
        ILogger<GatewayConnectionHandler> logger)
    {
        _options = options;
        _actionPolicy = actionPolicy;
        _logger = logger;
    }

    public async Task RunAsync(
        WebSocket socket,
        string remoteAddress,
        CancellationToken cancellationToken)
    {
        using var writeLock = new SemaphoreSlim(1, 1);
        var rateLimiter = new FixedWindowRateLimiter(_options.RequestsPerMinute);
        var requestIds = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        AgenticNamedPipeClient? localClient = null;
        string? clientName = null;

        try
        {
            var authentication = await ReceiveAsync(socket, cancellationToken).ConfigureAwait(false);
            if (authentication is null)
            {
                return;
            }

            clientName = NormalizeClientName(authentication.ClientName);
            var suppliedToken = authentication.AuthenticationToken;
            authentication.AuthenticationToken = null;
            var authenticated = IsValidRequestId(authentication.RequestId) &&
                                authentication.Type == RemoteMessageTypes.Authenticate &&
                                GatewaySecurity.FixedTimeEquals(_options.AuthenticationToken, suppliedToken);
            suppliedToken = null;
            if (!authenticated)
            {
                _logger.LogWarning(
                    "Gateway authentication rejected. RemoteAddress={RemoteAddress} ClientName={ClientName}",
                    remoteAddress,
                    clientName);
                await SendErrorAsync(
                    socket,
                    authentication.RequestId,
                    "Authentication failed.",
                    writeLock,
                    cancellationToken).ConfigureAwait(false);
                await CloseAsync(socket, WebSocketCloseStatus.PolicyViolation, "Authentication failed.", cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            try
            {
                localClient = await AgenticNamedPipeClient.ConnectAsync(
                    _options.LocalAuthenticationToken,
                    _options.PipeName,
                    "AgenticUI.Gateway/" + clientName,
                    _options.NamedPipeConnectTimeoutMilliseconds,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is IOException or TimeoutException or UnauthorizedAccessException)
            {
                _logger.LogWarning(
                    exception,
                    "Gateway could not connect to the local AgenticUI endpoint. PipeName={PipeName}",
                    _options.PipeName);
                await SendErrorAsync(
                    socket,
                    authentication.RequestId,
                    "The local AgenticUI endpoint is unavailable.",
                    writeLock,
                    cancellationToken).ConfigureAwait(false);
                await CloseAsync(
                    socket,
                    WebSocketCloseStatus.InternalServerError,
                    "Local endpoint unavailable.",
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            await WebSocketProtocol.SendAsync(
                socket,
                new RemoteResponse
                {
                    RequestId = authentication.RequestId,
                    Type = RemoteMessageTypes.Authenticated
                },
                writeLock,
                cancellationToken).ConfigureAwait(false);
            localClient.EventReceived += OnLocalEvent;

            _logger.LogInformation(
                "Gateway client authenticated. RemoteAddress={RemoteAddress} ClientName={ClientName}",
                remoteAddress,
                clientName);

            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var request = await ReceiveAsync(socket, cancellationToken).ConfigureAwait(false);
                if (request is null)
                {
                    break;
                }

                if (!rateLimiter.TryAcquire(DateTimeOffset.UtcNow))
                {
                    await SendErrorAsync(
                        socket,
                        request.RequestId,
                        "Request rate limit exceeded.",
                        writeLock,
                        cancellationToken).ConfigureAwait(false);
                    _logger.LogWarning(
                        "Gateway rate limit exceeded. RemoteAddress={RemoteAddress} ClientName={ClientName}",
                        remoteAddress,
                        clientName);
                    continue;
                }

                if (!IsValidRequestId(request.RequestId) ||
                    requestIds.Count >= MaximumRememberedRequestIds ||
                    !requestIds.TryAdd(request.RequestId, 0))
                {
                    await SendErrorAsync(
                        socket,
                        request.RequestId,
                        "Invalid or duplicate request ID.",
                        writeLock,
                        cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var response = await ForwardAsync(localClient, request, cancellationToken).ConfigureAwait(false);
                await WebSocketProtocol.SendAsync(socket, response, writeLock, cancellationToken)
                    .ConfigureAwait(false);
                Audit(remoteAddress, clientName, request, response);
            }

            void OnLocalEvent(AgenticUI.AgenticEvent message) =>
                _ = RelayLocalEventAsync(message);

            async Task RelayLocalEventAsync(AgenticUI.AgenticEvent message)
            {
                try
                {
                    await WebSocketProtocol.SendAsync(
                        socket,
                        new RemoteResponse { Type = RemoteMessageTypes.Event, Event = message },
                        writeLock,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    _logger.LogDebug(
                        exception,
                        "Gateway event relay stopped. RemoteAddress={RemoteAddress} ClientName={ClientName}",
                        remoteAddress,
                        clientName);
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException or WebSocketException or OperationCanceledException or
            TimeoutException or UnauthorizedAccessException)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    exception,
                    "Gateway connection ended. RemoteAddress={RemoteAddress} ClientName={ClientName}",
                    remoteAddress,
                    clientName);
            }
        }
        finally
        {
            localClient?.Dispose();
            await CloseAsync(socket, WebSocketCloseStatus.NormalClosure, "Connection closed.", CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    private async Task<RemoteRequest?> ReceiveAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        try
        {
            return await WebSocketProtocol.ReceiveRequestAsync(
                socket,
                _options.MaximumMessageLength,
                cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return new RemoteRequest { RequestId = "", Type = "invalid" };
        }
    }

    private async Task<RemoteResponse> ForwardAsync(
        AgenticNamedPipeClient localClient,
        RemoteRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (request.Type)
            {
                case RemoteMessageTypes.ListControls:
                    {
                        var response = await localClient.ListControlsAsync(request.IncludeHidden, cancellationToken)
                            .ConfigureAwait(false);
                        response.RequestId = request.RequestId;
                        return response;
                    }
                case RemoteMessageTypes.Execute when request.Command is not null:
                    if (!_actionPolicy.IsAllowed(request.Command.Action))
                    {
                        return Error(request.RequestId, "Action is not allowed by the gateway policy.");
                    }

                    var result = await localClient.ExecuteAsync(request.Command, cancellationToken)
                        .ConfigureAwait(false);
                    result.RequestId = request.RequestId;
                    return result;
                default:
                    return Error(request.RequestId, "Unsupported request type.");
            }
        }
        catch (Exception exception) when (
            exception is IOException or TimeoutException or ObjectDisposedException or OperationCanceledException)
        {
            return Error(request.RequestId, "The local AgenticUI endpoint is unavailable.");
        }
    }

    private void Audit(
        string remoteAddress,
        string clientName,
        RemoteRequest request,
        RemoteResponse response)
    {
        var succeeded = response.Type != RemoteMessageTypes.Error &&
                        response.Result?.Succeeded != false;
        _logger.LogInformation(
            "Gateway request completed. RemoteAddress={RemoteAddress} ClientName={ClientName} RequestId={RequestId} RequestType={RequestType} ControlId={ControlId} Action={Action} Succeeded={Succeeded}",
            remoteAddress,
            clientName,
            request.RequestId,
            request.Type,
            NormalizeLogValue(request.Command?.ControlId, 200),
            NormalizeLogValue(request.Command?.Action, 100),
            succeeded);
    }

    private static string NormalizeClientName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unnamed";
        }

        return NormalizeLogValue(value, 100) ?? "unnamed";
    }

    private static bool IsValidRequestId(string requestId) =>
        !string.IsNullOrWhiteSpace(requestId) &&
        requestId.Length <= 128 &&
        requestId.All(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or ':');

    private static string? NormalizeLogValue(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return new string(value
            .Where(character => !char.IsControl(character))
            .Take(maximumLength)
            .ToArray());
    }

    private static RemoteResponse Error(string? requestId, string message) =>
        new() { RequestId = requestId, Type = RemoteMessageTypes.Error, Error = message };

    private static Task SendErrorAsync(
        WebSocket socket,
        string? requestId,
        string message,
        SemaphoreSlim writeLock,
        CancellationToken cancellationToken) =>
        WebSocketProtocol.SendAsync(socket, Error(requestId, message), writeLock, cancellationToken);

    private static async Task CloseAsync(
        WebSocket socket,
        WebSocketCloseStatus status,
        string description,
        CancellationToken cancellationToken)
    {
        if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
        {
            return;
        }

        try
        {
            await socket.CloseAsync(status, description, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
        }
    }
}
