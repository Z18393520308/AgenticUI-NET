using System.Net;
using System.Net.Sockets;
using AgenticUI.Remote;
using Xunit;

namespace AgenticUI.Remote.Net48.Tests;

public sealed class PairingTests
{
    // 真实 net48：兼容 WebSocket 服务端、SslStream 客户端、Schannel 和 DPAPI。
    [Fact]
    public async Task PairAndReconnectWithoutHttpSysOrCertificateInstallation()
    {
        var directory = Path.Combine(Path.GetTempPath(), "aui-net48-" + Guid.NewGuid().ToString("N"));
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var options = new AgenticHostOptions
        {
            Local = new AgenticLocalHostOptions { PipeName = "net48-" + Guid.NewGuid().ToString("N"),
                TokenEnvironmentVariable = "AUI_TEST_" + Guid.NewGuid().ToString("N") },
            Network = new AgenticNetworkHostOptions { Enabled = true, ListenUrl = "https://localhost:" + port,
                StateDirectory = Path.Combine(directory, "server"), TokenEnvironmentVariable = "AUI_TEST_" + Guid.NewGuid().ToString("N") }
        };
        var store = new AgenticPairingStore(Path.Combine(directory, "client"));
        var endpoint = new Uri("wss://localhost:" + port + "/agenticui");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            string fingerprint;
            using (var host = AgenticApplicationHost.Start(options))
            {
                Assert.Null(host.LastError);
                var pairing = host.Pairing!;
                fingerprint = pairing.CertificateFingerprint;
                var code = pairing.BeginPairing();
                using var client = await AgenticWebSocketClient.ConnectPairedAsync(endpoint, prompt =>
                {
                    Assert.Equal(fingerprint, prompt.CertificateFingerprint);
                    return Task.FromResult<string?>(code);
                }, store, timeout.Token);
                Assert.Equal(RemoteMessageTypes.Controls, (await client.ListControlsAsync(timeout.Token)).Type);
            }
            using (var host = AgenticApplicationHost.Start(options))
            {
                Assert.Null(host.LastError);
                Assert.Equal(fingerprint, host.Pairing!.CertificateFingerprint);
                using var client = await AgenticWebSocketClient.ConnectPairedAsync(endpoint,
                    _ => throw new InvalidOperationException("不应重复配对"), store, timeout.Token);
                Assert.Equal(RemoteMessageTypes.Controls, (await client.ListControlsAsync(timeout.Token)).Type);
            }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
