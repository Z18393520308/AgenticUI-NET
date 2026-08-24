using System.Net.WebSockets;
using System.Text.Json;
using AgenticUI;
using AgenticUI.Remote;

namespace AgenticUI.Gateway;

internal static class WebSocketProtocol
{
    public static async Task<RemoteRequest?> ReceiveRequestAsync(
        WebSocket socket,
        int maximumMessageLength,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        var buffer = new byte[8192];

        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus.InvalidMessageType,
                    "Only JSON text messages are supported.",
                    cancellationToken).ConfigureAwait(false);
                return null;
            }

            if (stream.Length + result.Count > maximumMessageLength)
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus.MessageTooBig,
                    "Message exceeds the configured size limit.",
                    cancellationToken).ConfigureAwait(false);
                return null;
            }

            await stream.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken).ConfigureAwait(false);
            if (result.EndOfMessage)
            {
                break;
            }
        }

        stream.Position = 0;
        return await JsonSerializer.DeserializeAsync<RemoteRequest>(
            stream,
            AgenticJson.Options,
            cancellationToken).ConfigureAwait(false);
    }

    public static Task SendAsync(
        WebSocket socket,
        RemoteResponse response,
        SemaphoreSlim writeLock,
        CancellationToken cancellationToken) =>
        SendCoreAsync(socket, response, writeLock, cancellationToken);

    private static async Task SendCoreAsync(
        WebSocket socket,
        RemoteResponse response,
        SemaphoreSlim writeLock,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(response, AgenticJson.Options);
        await writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (socket.State == WebSocketState.Open)
            {
                await socket.SendAsync(
                    payload,
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            writeLock.Release();
        }
    }
}
