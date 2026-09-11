using System.Net;
using System.Net.Sockets;
using AgenticUI.Remote;
using Xunit;

namespace AgenticUI.Gateway.Tests;

public sealed class PairingIntegrationTests : IDisposable
{
    [Fact]
    public async Task LegacyCertificateBypassCannotBeUsedForNetworkEndpoints()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => AgenticWebSocketClient.ConnectAsync(
            new Uri("wss://example.invalid:7443/agenticui"), "test-token", skipTlsValidationForDevelopment: true));
    }
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "aui-pairing-" + Guid.NewGuid().ToString("N"));
    private AgenticHostOptions Options()
    {
        using var port = new TcpListener(IPAddress.Loopback, 0); port.Start();
        var number = ((IPEndPoint)port.LocalEndpoint).Port;
        return new AgenticHostOptions
        {
            Local = new() { PipeName = "pairing-" + Guid.NewGuid().ToString("N"), TokenEnvironmentVariable = "AUI_TEST_" + Guid.NewGuid().ToString("N") },
            Network = new() { Enabled = true, ListenUrl = "https://localhost:" + number,
                StateDirectory = Path.Combine(_directory, "server"), TokenEnvironmentVariable = "AUI_TEST_" + Guid.NewGuid().ToString("N") }
        };
    }

    [Fact]
    public async Task PairReconnectAfterRestartAndRevokeOverRealTls()
    {
        var options = Options();
        var endpoint = new Uri(options.Network.ListenUrl.Replace("https:", "wss:") + "/agenticui");
        var store = new AgenticPairingStore(Path.Combine(_directory, "client"));
        string fingerprint;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using (var host = AgenticApplicationHost.Start(options))
        {
            Assert.Null(host.LastError); Assert.True(host.NetworkRunning);
            var pairing = host.Pairing!;
            fingerprint = pairing.CertificateFingerprint;
            var code = pairing.BeginPairing();
            using var client = await AgenticWebSocketClient.ConnectPairedAsync(endpoint, prompt =>
            {
                Assert.Equal(fingerprint, prompt.CertificateFingerprint);
                return Task.FromResult<string?>(code);
            }, store, timeout.Token);
            Assert.Equal(RemoteMessageTypes.Controls, (await client.ListControlsAsync(timeout.Token)).Type);
            Assert.Equal(1, pairing.PairedClientCount);
        }
        using (var host = AgenticApplicationHost.Start(options))
        {
            Assert.Null(host.LastError);
            Assert.Equal(fingerprint, host.Pairing!.CertificateFingerprint);
            using var client = await AgenticWebSocketClient.ConnectPairedAsync(endpoint,
                _ => throw new Exception("已配对端点不应再弹窗"), store, timeout.Token);
            Assert.Equal(RemoteMessageTypes.Controls, (await client.ListControlsAsync(timeout.Token)).Type);
            host.Pairing.RevokeAllClients();
            await Assert.ThrowsAnyAsync<Exception>(() => client.ListControlsAsync(timeout.Token));
            Assert.Equal(0, host.Pairing.PairedClientCount);
        }
    }

    [Fact]
    public async Task CertificateChangeIsNotSilentlyRepaired()
    {
        var options = Options();
        var endpoint = new Uri(options.Network.ListenUrl.Replace("https:", "wss:") + "/agenticui");
        var store = new AgenticPairingStore(Path.Combine(_directory, "client"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using (var host = AgenticApplicationHost.Start(options))
        {
            var code = host.Pairing!.BeginPairing();
            using var client = await AgenticWebSocketClient.ConnectPairedAsync(endpoint, _ => Task.FromResult<string?>(code), store, timeout.Token);
        }
        options.Network.StateDirectory = Path.Combine(_directory, "other-server");
        using (var host = AgenticApplicationHost.Start(options))
        {
            Assert.Null(host.LastError);
            var askedAgain = false;
            await Assert.ThrowsAnyAsync<Exception>(() => AgenticWebSocketClient.ConnectPairedAsync(endpoint,
                _ => { askedAgain = true; return Task.FromResult<string?>(host.Pairing!.BeginPairing()); }, store, timeout.Token));
            Assert.False(askedAgain);
            Assert.Equal(0, host.Pairing!.PairedClientCount);
        }
    }

    [Fact]
    public async Task CancellationAndWrongCodeDoNotCreateTrustedRecord()
    {
        var options = Options();
        var endpoint = new Uri(options.Network.ListenUrl.Replace("https:", "wss:") + "/agenticui");
        var store = new AgenticPairingStore(Path.Combine(_directory, "client"));
        using var host = AgenticApplicationHost.Start(options);
        host.Pairing!.BeginPairing();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AgenticWebSocketClient.ConnectPairedAsync(endpoint,
            _ => Task.FromResult<string?>(null), store, timeout.Token));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => AgenticWebSocketClient.ConnectPairedAsync(endpoint,
            _ => Task.FromResult<string?>("wrong"), store, timeout.Token));
        Assert.Null(store.Load(endpoint)); Assert.Equal(0, host.Pairing.PairedClientCount);
    }

    [Fact]
    public void PairingCodeIsSingleUseBoundedAndCancelable()
    {
        var now = DateTimeOffset.UtcNow;
        using var pairing = new AgenticPairingService(Path.Combine(_directory, "server"), () => now);
        Assert.Null(pairing.Exchange("not-open"));
        var code = pairing.BeginPairing();
        for (var i = 0; i < 5; i++) Assert.Null(pairing.Exchange("wrong"));
        Assert.Null(pairing.Exchange(code));
        code = pairing.BeginPairing();
        var token = pairing.Exchange(code);
        Assert.NotNull(token); Assert.True(pairing.IsAuthorized(token));
        Assert.Null(pairing.Exchange(code));
        code = pairing.BeginPairing(); pairing.CancelPairing();
        Assert.Null(pairing.Exchange(code));
        pairing.RevokeAllClients(); Assert.False(pairing.IsAuthorized(token));
        code = pairing.BeginPairing(); now = now.AddMinutes(4);
        Assert.Null(pairing.Exchange(code));
    }

    [Fact]
    public async Task DiscoveryBroadcastAnnouncesRunningTlsEndpoint()
    {
        var options = Options();
        using var reservation = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)reservation.Client.LocalEndPoint!).Port;
        reservation.Dispose();
        options.Discovery.Enabled = true; options.Discovery.Port = port; options.Discovery.IntervalSeconds = 2;
        var scanning = AgenticGatewayDiscovery.ScanAsync(port, TimeSpan.FromSeconds(3));
        using var host = AgenticApplicationHost.Start(options);
        Assert.Null(host.LastError);
        var entries = await scanning;
        var entry = Assert.Single(entries);
        Assert.Equal(options.Network.ListenUrl.Replace("https:", "wss:") + "/agenticui", entry.Announcement.WebSocketUrl);
        Assert.Equal(host.Pairing!.CertificateFingerprint, entry.Announcement.CertificateFingerprint);
    }

    [Fact]
    public async Task HttpHeadersAreBoundedAndDuplicatesRejected()
    {
        using var tooLarge = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(new string('A', 8193)));
        await Assert.ThrowsAsync<InvalidDataException>(() => TlsWebSocketTransport.ReadHeadersAsync(tooLarge, default));
        Assert.Throws<InvalidDataException>(() => new TlsWebSocketTransport.Headers(
            "GET /agenticui HTTP/1.1\r\nHost: first\r\nHost: second\r\n\r\n"));
    }

    public void Dispose()
    {
        AgenticApplicationHost.Current?.Dispose();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
