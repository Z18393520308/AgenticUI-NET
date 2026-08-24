using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using AgenticUI;

namespace AgenticUI.Remote;

public sealed class AgenticGatewayDiscoveryAnnouncement
{
    public const string ProtocolVersion = "AgenticUI.Discovery.v1";

    public string Protocol { get; set; } = ProtocolVersion;

    public string InstanceId { get; set; } = "";

    public string ServiceName { get; set; } = "";

    public string WebSocketUrl { get; set; } = "";

    public string Version { get; set; } = "";

    public DateTimeOffset Timestamp { get; set; }

    public string[] Capabilities { get; set; } = { "wss", "semantic-ui" };
}

public sealed class AgenticGatewayDiscoveryEntry
{
    public IPAddress SourceAddress { get; init; } = IPAddress.None;

    public AgenticGatewayDiscoveryAnnouncement Announcement { get; init; } = new();

    public string DisplayText =>
        $"{Announcement.ServiceName}  ·  {Announcement.WebSocketUrl}  ·  {SourceAddress}";
}

public static class AgenticGatewayDiscovery
{
    public const int DefaultPort = 47731;

    public static async Task<IReadOnlyList<AgenticGatewayDiscoveryEntry>> ScanAsync(
        int port = DefaultPort,
        TimeSpan? listenDuration = null,
        CancellationToken cancellationToken = default)
    {
        listenDuration ??= TimeSpan.FromSeconds(6);
        var results = new Dictionary<string, AgenticGatewayDiscoveryEntry>(StringComparer.OrdinalIgnoreCase);

        using var client = CreateListeningClient(port);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(listenDuration.Value);

        while (!timeout.Token.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await ReceiveUdpAsync(client, timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (!TryParseAnnouncement(result.Buffer, out var announcement))
            {
                continue;
            }

            var key = $"{announcement.InstanceId}|{announcement.WebSocketUrl}";
            results[key] = new AgenticGatewayDiscoveryEntry
            {
                SourceAddress = result.RemoteEndPoint.Address,
                Announcement = announcement
            };
        }

        return results.Values
            .OrderBy(entry => entry.Announcement.ServiceName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static async Task ListenContinuousAsync(
        int port,
        Action<AgenticGatewayDiscoveryEntry> onDiscovered,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateListeningClient(port);
        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await ReceiveUdpAsync(client, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (!TryParseAnnouncement(result.Buffer, out var announcement))
            {
                continue;
            }

            onDiscovered(new AgenticGatewayDiscoveryEntry
            {
                SourceAddress = result.RemoteEndPoint.Address,
                Announcement = announcement
            });
        }
    }

    public static bool TryParseAnnouncement(
        ReadOnlySpan<byte> payload,
        out AgenticGatewayDiscoveryAnnouncement announcement)
    {
        announcement = null!;
        try
        {
            var parsed = JsonSerializer.Deserialize<AgenticGatewayDiscoveryAnnouncement>(
                payload,
                AgenticJson.Options);
            if (parsed is null ||
                !string.Equals(parsed.Protocol, AgenticGatewayDiscoveryAnnouncement.ProtocolVersion, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(parsed.WebSocketUrl))
            {
                return false;
            }

            announcement = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static async Task<UdpReceiveResult> ReceiveUdpAsync(
        UdpClient client,
        CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        return await client.ReceiveAsync(cancellationToken).ConfigureAwait(false);
#else
        var receiveTask = client.ReceiveAsync();
        if (!cancellationToken.CanBeCanceled)
        {
            return await receiveTask.ConfigureAwait(false);
        }

        var cancelTask = Task.Delay(Timeout.Infinite, cancellationToken);
        var completed = await Task.WhenAny(receiveTask, cancelTask).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await receiveTask.ConfigureAwait(false);
#endif
    }

    private static UdpClient CreateListeningClient(int port)
    {
        var client = new UdpClient();
        client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        client.Client.Bind(new IPEndPoint(IPAddress.Any, port));
        return client;
    }
}
