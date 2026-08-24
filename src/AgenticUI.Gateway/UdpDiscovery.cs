using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using AgenticUI;

namespace AgenticUI.Gateway;

public sealed class GatewayDiscoveryAnnouncement
{
    public string Protocol { get; set; } = "AgenticUI.Discovery.v1";
    public string InstanceId { get; set; } = "";
    public string ServiceName { get; set; } = "";
    public string WebSocketUrl { get; set; } = "";
    public string Version { get; set; } = "";
    public DateTimeOffset Timestamp { get; set; }
    public string[] Capabilities { get; set; } = ["wss", "semantic-ui"];
}

internal sealed class UdpDiscoveryBroadcaster : BackgroundService
{
    private readonly GatewayOptions _options;
    private readonly ILogger<UdpDiscoveryBroadcaster> _logger;
    private readonly string _instanceId;

    public UdpDiscoveryBroadcaster(
        GatewayOptions options,
        ILogger<UdpDiscoveryBroadcaster> logger)
    {
        _options = options;
        _logger = logger;
        _instanceId = string.IsNullOrWhiteSpace(options.Discovery.InstanceId)
            ? Guid.NewGuid().ToString("N")
            : options.Discovery.InstanceId;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Discovery.Enabled)
        {
            _logger.LogInformation("UDP discovery is disabled.");
            return;
        }

        using var client = new UdpClient { EnableBroadcast = true };
        var destination = new IPEndPoint(IPAddress.Broadcast, _options.Discovery.Port);
        _logger.LogInformation(
            "UDP discovery broadcasting enabled. Port={Port} IntervalSeconds={IntervalSeconds}",
            _options.Discovery.Port,
            _options.Discovery.IntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var payload = CreatePayload();
            await client.SendAsync(payload, destination, stoppingToken).ConfigureAwait(false);
            await Task.Delay(
                TimeSpan.FromSeconds(_options.Discovery.IntervalSeconds),
                stoppingToken).ConfigureAwait(false);
        }
    }

    internal byte[] CreatePayload()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "unknown";
        return JsonSerializer.SerializeToUtf8Bytes(
            new GatewayDiscoveryAnnouncement
            {
                InstanceId = _instanceId,
                ServiceName = _options.Discovery.ServiceName,
                WebSocketUrl = _options.Discovery.PublicWebSocketUrl,
                Version = version,
                Timestamp = DateTimeOffset.UtcNow
            },
            AgenticJson.Options);
    }
}

internal static class UdpDiscoveryListener
{
    public static async Task RunAsync(int port, CancellationToken cancellationToken)
    {
        using var client = new UdpClient(port);
        Console.WriteLine($"Listening for AgenticUI discovery announcements on UDP {port}...");

        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await client.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var announcement = JsonSerializer.Deserialize<GatewayDiscoveryAnnouncement>(
                    result.Buffer,
                    AgenticJson.Options);
                if (announcement?.Protocol == "AgenticUI.Discovery.v1")
                {
                    Console.WriteLine(
                        $"{result.RemoteEndPoint.Address}  {announcement.ServiceName}  {announcement.WebSocketUrl}");
                }
            }
            catch (JsonException)
            {
                // Ignore unrelated UDP traffic on the discovery port.
            }
        }
    }
}
