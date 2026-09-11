using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AgenticUI;
using AgenticUI.Remote;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AgenticUI.Gateway.Tests;

public sealed class WebSocketIntegrationTests
{
    [Fact]
    public async Task WssForwardsDynamicGuidanceAcrossRequestWindowAndCleansDisconnectedSession()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        // Import the test key through PKCS#12 so Windows Schannel can use it for server TLS.
        using var certificate = new X509Certificate2(generated.Export(X509ContentType.Pfx));
        var registry = new AgenticControlRegistry(); var bus = new AgenticEventBus();
        var target = new Target(); registry.Register(target, "target");
        var pipeName = "aui-" + Guid.NewGuid().ToString("N");
        using var pipe = new AgenticNamedPipeServer(pipeName, registry, events: bus); pipe.Start();
        var networkToken = AgenticRemoteSecurity.CreateToken();
        var builder = WebApplication.CreateBuilder();
        // 测试只监听自己的回环临时端口，不能继承 Gateway appsettings 中的生产端点。
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(certificate)));
        await using var app = builder.Build();
        app.UseWebSockets();
        app.Map("/agenticui", async context =>
        {
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
                await EmbeddedGatewaySession.RunAsync(socket, new AgenticHostOptions
                {
                    Local = new() { PipeName = pipeName },
                    Network = new() { RequestsPerMinute = 10000 }
                }, pipe.AuthenticationToken, networkToken, context.RequestAborted);
        });
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            var uri = new Uri(address.Replace("https://", "wss://", StringComparison.Ordinal) + "/agenticui");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
            using var client = await AgenticWebSocketClient.ConnectAsync(uri, networkToken,
                skipTlsValidationForDevelopment: true, cancellationToken: timeout.Token);
            for (var index = 0; index < 2050; index++)
            {
                var controls = await client.ListControlsAsync(false, timeout.Token);
                Assert.Equal(RemoteMessageTypes.Controls, controls.Type);
            }
            var result = await client.ExecuteAsync(new AgenticCommand { ControlId = "target", Action = "highlight",
                Arguments = { ["showBubble"] = true, ["hint"] = "WSS 动态提示", ["guidanceId"] = "step-2" } }, timeout.Token);
            Assert.True(result.Result!.Succeeded, result.Result.Error);
            Assert.Equal("WSS 动态提示", target.LastGuidance!.Hint);
            Assert.False(string.IsNullOrEmpty(target.SessionId));
            var denied = await client.ExecuteAsync(new AgenticCommand { ControlId = "target", Action = "setText" }, timeout.Token);
            Assert.Equal(RemoteMessageTypes.Error, denied.Type);
            client.Dispose();
            Assert.Equal(target.SessionId, await target.Cleared.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally { await app.StopAsync(); }
    }

    private sealed class Target : IAgenticControl, IAgenticGuidanceControl
    {
        public AgenticGuidanceOptions? LastGuidance { get; private set; }
        public string? SessionId { get; private set; }
        public TaskCompletionSource<string> Cleared { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public AgenticControlDescriptor Describe() => new() { Id = "target", Actions = new[] { "highlight" },
            Capabilities = new[] { AgenticGuidanceOptions.Capability } };
        public bool IsRemotelyDiscoverable() => true;
        public Task<AgenticCommandResult> ExecuteAsync(AgenticCommand command, CancellationToken cancellationToken = default)
        {
            SessionId = command.SessionId; LastGuidance = AgenticGuidanceOptions.FromCommand(command);
            return Task.FromResult(AgenticCommandResult.Success(command.RequestId, Describe()));
        }
        public Task ClearGuidanceAsync(string sessionId) { Cleared.TrySetResult(sessionId); return Task.CompletedTask; }
    }
}
