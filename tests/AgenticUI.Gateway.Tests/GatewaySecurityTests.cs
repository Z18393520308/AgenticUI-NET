using System.Text;
using AgenticUI.Remote;
using Xunit;

namespace AgenticUI.Gateway.Tests;

public sealed class GatewaySecurityTests
{
    [Theory]
    [InlineData("http://localhost:7443")]
    [InlineData("https://0.0.0.0:7443")]
    public void InsecureListenAddressIsRejected(string address)
    {
        var options = new AgenticHostOptions();
        options.Network.Enabled = true;
        options.Network.ListenUrl = address;
        Assert.Throws<InvalidDataException>(options.Validate);
    }

    [Fact]
    public void DiscoveryRequiresWss()
    {
        var options = new AgenticHostOptions();
        options.Network.Enabled = true;
        options.Discovery.Enabled = true;
        options.Discovery.PublicWebSocketUrl = "ws://localhost:7443/agenticui";
        Assert.Throws<InvalidDataException>(options.Validate);
    }

    [Fact]
    public void DefaultPolicyRejectsMutationAndWildcardIsInvalid()
    {
        var options = new AgenticHostOptions();
        Assert.Contains("getRows", options.Network.AllowedActions);
        Assert.Contains("highlightCell", options.Network.AllowedActions);
        Assert.DoesNotContain("setText", options.Network.AllowedActions);
        Assert.DoesNotContain("deleteRow", options.Network.AllowedActions);
        options.Network.Enabled = true;
        options.Network.AllowedActions = ["*"];
        Assert.Throws<InvalidDataException>(options.Validate);
    }

    [Fact]
    public void DiscoveryIsCompatibleAndContainsNoSecrets()
    {
        var options = new AgenticHostOptions();
        options.Local.PipeName = "private-local-pipe";
        options.Discovery.PublicWebSocketUrl = "wss://gateway.example.test:7443/agenticui";
        using var gateway = new EmbeddedGateway(options, "secret-local-token", "secret-network-token");
        var bytes = gateway.CreateAnnouncement();
        var json = Encoding.UTF8.GetString(bytes);
        Assert.True(AgenticGatewayDiscovery.TryParseAnnouncement(bytes, out var announcement));
        Assert.Equal(options.Discovery.PublicWebSocketUrl, announcement.WebSocketUrl);
        Assert.DoesNotContain("secret", json);
        Assert.DoesNotContain(options.Local.PipeName, json);
    }

    [Fact]
    public void RequestsAreRateLimited()
    {
        var window = new EmbeddedGateway.RequestWindow(2);
        Assert.True(window.TryAcquire());
        Assert.True(window.TryAcquire());
        Assert.False(window.TryAcquire());
    }
}
