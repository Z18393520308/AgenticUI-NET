using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using AgenticUI;
using AgenticUI.Remote;

namespace AgenticUI.Gateway;

internal sealed class UdpDiscoveryBroadcaster : BackgroundService
{
    private readonly GatewayOptions _options;
    private readonly ILogger<UdpDiscoveryBroadcaster> _logger;
    private readonly string _instanceId;
    private readonly IPEndPoint[] _destinations;

    public UdpDiscoveryBroadcaster(
        GatewayOptions options,
        ILogger<UdpDiscoveryBroadcaster> logger)
    {
        _options = options;
        _logger = logger;
        _instanceId = string.IsNullOrWhiteSpace(options.Discovery.InstanceId)
            ? Guid.NewGuid().ToString("N")
            : options.Discovery.InstanceId;
        _destinations = GetDiscoveryDestinations(options.Discovery.Port).ToArray();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Discovery.Enabled)
        {
            _logger.LogInformation("UDP discovery is disabled.");
            return;
        }

        using var client = new UdpClient { EnableBroadcast = true };
        _logger.LogInformation(
            "UDP discovery broadcasting enabled. Port={Port} IntervalSeconds={IntervalSeconds} Destinations={DestinationCount}",
            _options.Discovery.Port,
            _options.Discovery.IntervalSeconds,
            _destinations.Length);

        while (!stoppingToken.IsCancellationRequested)
        {
            var payload = CreatePayload();
            foreach (var destination in _destinations)
            {
                await client.SendAsync(payload, destination, stoppingToken).ConfigureAwait(false);
            }

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
            new AgenticGatewayDiscoveryAnnouncement
            {
                InstanceId = _instanceId,
                ServiceName = _options.Discovery.ServiceName,
                WebSocketUrl = _options.Discovery.PublicWebSocketUrl,
                Version = version,
                Timestamp = DateTimeOffset.UtcNow
            },
            AgenticJson.Options);
    }

    internal static IEnumerable<IPEndPoint> GetDiscoveryDestinations(int port)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var address in GetDiscoveryAddresses())
        {
            if (!seen.Add(address.ToString()))
            {
                continue;
            }

            yield return new IPEndPoint(address, port);
        }
    }

    private static IEnumerable<IPAddress> GetDiscoveryAddresses()
    {
        yield return IPAddress.Broadcast;
        yield return IPAddress.Loopback;

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up ||
                nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            foreach (var uni in nic.GetIPProperties().UnicastAddresses)
            {
                if (uni.Address.AddressFamily != AddressFamily.InterNetwork ||
                    uni.IPv4Mask is null)
                {
                    continue;
                }

                var ip = uni.Address.GetAddressBytes();
                var mask = uni.IPv4Mask.GetAddressBytes();
                var broadcast = new byte[4];
                for (var i = 0; i < 4; i++)
                {
                    broadcast[i] = (byte)(ip[i] | ~mask[i]);
                }

                yield return new IPAddress(broadcast);
            }
        }
    }
}

internal static class UdpDiscoveryListener
{
    public static Task RunAsync(int port, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Listening for AgenticUI discovery announcements on UDP {port}...");
        return AgenticGatewayDiscovery.ListenContinuousAsync(
            port,
            entry => Console.WriteLine($"{entry.SourceAddress}  {entry.DisplayText}"),
            cancellationToken);
    }
}
