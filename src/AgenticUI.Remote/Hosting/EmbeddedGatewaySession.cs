using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;

namespace AgenticUI.Remote;

/// <summary>重用官方 Pipe 客户端转发：认证、会话隔离和宿主业务授权不走旁路。</summary>
internal static class EmbeddedGatewaySession
{
    internal static async Task RunAsync(WebSocket socket, AgenticHostOptions options, string pipeToken,
        string networkToken, CancellationToken stoppingToken, AgenticPairingService? pairing = null)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var abort = lifetime.Token.Register(socket.Abort);
        // 所有响应和事件共用单一发送者；慢客户端只能积压有限数量消息。
        using var outbound = new BlockingCollection<byte[]>(32);
        AgenticNamedPipeClient? local = null;
        var sender = Task.Run(async () =>
        {
            try
            {
                foreach (var bytes in outbound.GetConsumingEnumerable(lifetime.Token))
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(10));
                    await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or ObjectDisposedException)
            { lifetime.Cancel(); }
        });

        void Enqueue(RemoteResponse response)
        {
            try
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(response, AgenticJson.Options);
                if (bytes.Length > options.Network.MaximumMessageLength || !outbound.TryAdd(bytes)) lifetime.Cancel();
            }
            catch (InvalidOperationException) { lifetime.Cancel(); }
        }
        void OnEvent(AgenticEvent message) => Enqueue(new RemoteResponse { Type = RemoteMessageTypes.Event, Event = message });
        void OnFault(Exception exception) => lifetime.Cancel();

        try
        {
            using var authentication = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            authentication.CancelAfter(TimeSpan.FromSeconds(10));
            var request = await ReceiveAsync(socket, options.Network.MaximumMessageLength, authentication.Token).ConfigureAwait(false);
            if (request is null || !ValidId(request.RequestId)) return;
            if (request.Type == RemoteMessageTypes.Pair)
            {
                var issued = pairing?.Exchange(request.AuthenticationToken);
                Enqueue(issued is null ? Error(request.RequestId, "配对码无效、已过期或配对窗口已关闭。")
                    : new RemoteResponse { Type = RemoteMessageTypes.Paired, RequestId = request.RequestId, PairingToken = issued });
                return;
            }
            var credential = request.AuthenticationToken;
            bool Authorized() => (!string.IsNullOrEmpty(networkToken) && AgenticRemoteSecurity.FixedTimeEquals(networkToken, credential))
                || pairing?.IsAuthorized(credential) == true;
            if (request.Type != RemoteMessageTypes.Authenticate || !Authorized()) return;
            request.AuthenticationToken = null;
            local = await AgenticNamedPipeClient.ConnectAsync(pipeToken, options.Local.PipeName,
                "AgenticUI.EmbeddedGateway", cancellationToken: authentication.Token).ConfigureAwait(false);
            Enqueue(new RemoteResponse { Type = RemoteMessageTypes.Authenticated, RequestId = request.RequestId });
            local.EventReceived += OnEvent;
            local.ConnectionFaulted += OnFault;
            var seen = new HashSet<string>(StringComparer.Ordinal) { request.RequestId };
            var order = new Queue<string>(); order.Enqueue(request.RequestId);
            var rate = new EmbeddedGateway.RequestWindow(options.Network.RequestsPerMinute);
            while (!lifetime.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                request = await ReceiveAsync(socket, options.Network.MaximumMessageLength, lifetime.Token).ConfigureAwait(false);
                if (request is null || !Authorized()) break;
                if (!rate.TryAcquire()) { Enqueue(Error(request.RequestId, "Request rate limit exceeded.")); break; }
                if (!ValidId(request.RequestId) || !seen.Add(request.RequestId))
                { Enqueue(Error(request.RequestId, "Invalid or duplicate request ID.")); continue; }
                order.Enqueue(request.RequestId);
                if (order.Count > 2048) seen.Remove(order.Dequeue());
                RemoteResponse response;
                using var commandTimeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                commandTimeout.CancelAfter(TimeSpan.FromSeconds(options.Network.CommandTimeoutSeconds));
                try
                {
                    switch (request.Type)
                    {
                        case RemoteMessageTypes.ListControls:
                            response = await local.ListControlsAsync(request.IncludeHidden, commandTimeout.Token).ConfigureAwait(false);
                            break;
                        case RemoteMessageTypes.Execute when request.Command is not null:
                            if (!options.Network.AllowedActions.Contains(request.Command.Action, StringComparer.OrdinalIgnoreCase))
                            { response = Error(request.RequestId, "Action is not allowed by the gateway policy."); break; }
                            response = await local.ExecuteAsync(request.Command, commandTimeout.Token).ConfigureAwait(false);
                            break;
                        default: response = Error(request.RequestId, "Unsupported request type."); break;
                    }
                }
                catch (OperationCanceledException) when (!lifetime.IsCancellationRequested)
                {
                    // 超时不代表业务未执行，不能自动重试写操作。
                    Enqueue(Error(request.RequestId, "Local command timed out; outcome is unknown. Do not retry automatically."));
                    break;
                }
                response.RequestId = request.RequestId;
                Enqueue(response);
                Trace.TraceInformation("AgenticUI embedded request {0}; action={1}; succeeded={2}",
                    request.RequestId, SafeAction(request.Command?.Action), response.Type != RemoteMessageTypes.Error && response.Result?.Succeeded != false);
            }
        }
        catch (Exception exception) when (exception is IOException or WebSocketException or OperationCanceledException or
            UnauthorizedAccessException or TimeoutException or ObjectDisposedException or JsonException)
        { Trace.TraceWarning("AgenticUI 进程内网关会话已断开；详细数据未记录。"); }
        finally
        {
            if (local is not null)
            {
                local.EventReceived -= OnEvent;
                local.ConnectionFaulted -= OnFault;
                local.Dispose(); // 服务端沿原 Pipe 会话清理引导。
            }
            outbound.CompleteAdding();
            // 先尝试发送最后一个错误/响应，但不会无限等待不读数据的远端。
            await Task.WhenAny(sender, Task.Delay(1000)).ConfigureAwait(false);
            lifetime.Cancel();
            await sender.ConfigureAwait(false);
        }
    }

    private static string SafeAction(string? action) => new((action ?? "").Where(c => char.IsLetterOrDigit(c) || c == '_').Take(80).ToArray());
    private static bool ValidId(string? id) => !string.IsNullOrWhiteSpace(id) && id!.Length <= 128 &&
        id.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' or ':');
    private static RemoteResponse Error(string? id, string error) => new() { RequestId = id, Type = RemoteMessageTypes.Error, Error = error };

    private static async Task<RemoteRequest?> ReceiveAsync(WebSocket socket, int limit, CancellationToken token)
    {
        using var stream = new MemoryStream();
        var bytes = new byte[8192];
        while (true)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(bytes), token).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            if (result.MessageType != WebSocketMessageType.Text || stream.Length + result.Count > limit)
                throw new WebSocketException("Invalid message type or size.");
            await stream.WriteAsync(bytes, 0, result.Count, token).ConfigureAwait(false);
            if (result.EndOfMessage) return JsonSerializer.Deserialize<RemoteRequest>(stream.ToArray(), AgenticJson.Options);
        }
    }
}
