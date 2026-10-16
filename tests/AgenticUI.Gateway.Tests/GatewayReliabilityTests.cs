using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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

public sealed class GatewayReliabilityTests
{
    [Fact]
    public async Task OutboundQueue_DropsEventBurstThenSendsNoticeAndKeepsResponses()
    {
        var queue = new EmbeddedGatewaySession.OutboundQueue();
        byte[] Text(string value) => Encoding.UTF8.GetBytes(value);
        for (var index = 0; index < EmbeddedGatewaySession.OutboundQueue.MaximumPendingEvents + 10; index++)
            queue.EnqueueEvent(Text("event"), dropped => Text("dropped:" + dropped));
        Assert.True(queue.TryEnqueueResponse(Text("response")));
        queue.Complete();

        var sent = new List<string>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (await queue.DequeueAsync(timeout.Token) is { } bytes) sent.Add(Encoding.UTF8.GetString(bytes));

        Assert.Equal(EmbeddedGatewaySession.OutboundQueue.MaximumPendingEvents, sent.Count(item => item == "event"));
        Assert.Contains("response", sent);
        Assert.Equal("dropped:10", sent.Last());
    }

    [Fact]
    public async Task WssKeepsSessionForOversizedResponsesAndEventBursts()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        using var certificate = new X509Certificate2(generated.Export(X509ContentType.Pfx));
        var registry = new AgenticControlRegistry(); var bus = new AgenticEventBus();
        var large = new SizedControl("large.grid", 0);
        registry.Register(large, "large.grid");
        var pipeName = "aui-" + Guid.NewGuid().ToString("N");
        using var pipe = new AgenticNamedPipeServer(pipeName, registry, events: bus); pipe.Start();
        var networkToken = AgenticRemoteSecurity.CreateToken();
        var builder = WebApplication.CreateBuilder();
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

            // 超过 1 MiB 的响应：返回同一请求的明确错误，连接保持可用。
            large.PayloadLength = 1_200_000;
            var oversized = await client.ListControlsAsync(false, timeout.Token);
            Assert.Equal(RemoteMessageTypes.Error, oversized.Type);
            Assert.Contains("message limit", oversized.Error);
            large.PayloadLength = 10;
            Assert.Equal(RemoteMessageTypes.Controls, (await client.ListControlsAsync(false, timeout.Token)).Type);

            // 事件突发：不断线，并收到 eventsDropped 提示后可继续请求。
            var dropped = new TaskCompletionSource<AgenticEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.EventReceived += message => { if (message.Name == AgenticEvents.EventsDropped) dropped.TrySetResult(message); };
            var data = new Dictionary<string, object?> { ["text"] = new string('y', 200) };
            for (var index = 0; index < 5000; index++)
                _ = bus.PublishAsync("large.grid", AgenticEvents.TextChanged, AgenticEventSource.User, data);
            await dropped.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.True(client.IsConnected);
            Assert.Equal(RemoteMessageTypes.Controls, (await client.ListControlsAsync(false, timeout.Token)).Type);
        }
        finally { await app.StopAsync(); }
    }

    private sealed class SizedControl(string id, int payloadLength) : IAgenticControl
    {
        public int PayloadLength { get; set; } = payloadLength;
        public AgenticControlDescriptor Describe() => new()
        {
            Id = id, Name = id, Kind = "dataGrid",
            State = new Dictionary<string, object?> { ["rows"] = new string('x', PayloadLength) }
        };
        public Task<AgenticCommandResult> ExecuteAsync(AgenticCommand command, CancellationToken cancellationToken = default) =>
            Task.FromResult(AgenticCommandResult.Success(command.RequestId, Describe()));
        public bool IsRemotelyDiscoverable() => true;
    }
}
