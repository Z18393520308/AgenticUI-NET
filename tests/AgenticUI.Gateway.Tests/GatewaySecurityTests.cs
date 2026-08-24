using System.Net;
using System.Text;
using AgenticUI;
using AgenticUI.Gateway;
using AgenticUI.Remote;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgenticUI.Gateway.Tests;

public sealed class GatewaySecurityTests
{
    [Fact]
    public void OptionsRequireTwoLongDifferentTokens()
    {
        var options = ValidOptions();
        options.LocalAuthenticationToken = options.AuthenticationToken;

        var errors = GatewayOptionsValidator.Validate(options);

        Assert.Contains(errors, error => error.Contains("must be different", StringComparison.Ordinal));
    }

    [Fact]
    public void DiscoveryRequiresWssWhenEnabled()
    {
        var options = ValidOptions();
        options.Discovery.Enabled = true;
        options.Discovery.PublicWebSocketUrl = "ws://192.168.1.10:7443/agenticui";

        var errors = GatewayOptionsValidator.Validate(options);

        Assert.Contains(errors, error => error.Contains("wss://", StringComparison.Ordinal));
    }

    [Fact]
    public void DefaultPolicyAllowsReadAndGuidanceButRejectsMutation()
    {
        var policy = new GatewayActionPolicy(new GatewayOptions().AllowedActions);

        Assert.True(policy.IsAllowed(AgenticActions.GetRows));
        Assert.True(policy.IsAllowed(AgenticActions.HighlightCell));
        Assert.False(policy.IsAllowed(AgenticActions.Click));
        Assert.False(policy.IsAllowed(AgenticActions.DeleteRow));
        Assert.False(policy.IsAllowed(AgenticActions.SetText));
    }

    [Fact]
    public void WildcardPolicyAllowsAllNonEmptyActions()
    {
        var policy = new GatewayActionPolicy(["*"]);

        Assert.True(policy.IsAllowed(AgenticActions.DeleteRow));
        Assert.False(policy.IsAllowed(""));
    }

    [Fact]
    public void AuthenticationComparisonUsesExactValue()
    {
        const string token = "0123456789abcdefghijklmnopqrstuvwxyz";

        Assert.True(GatewaySecurity.FixedTimeEquals(token, token));
        Assert.False(GatewaySecurity.FixedTimeEquals(token, token + "x"));
        Assert.False(GatewaySecurity.FixedTimeEquals(token, null));
    }

    [Fact]
    public void DiscoveryPayloadContainsNoTokensOrPipeName()
    {
        var options = ValidOptions();
        options.Discovery.Enabled = true;
        options.Discovery.PublicWebSocketUrl = "wss://gateway.example.test:7443/agenticui";
        var broadcaster = new UdpDiscoveryBroadcaster(
            options,
            NullLogger<UdpDiscoveryBroadcaster>.Instance);

        var json = Encoding.UTF8.GetString(broadcaster.CreatePayload());

        Assert.Contains("AgenticUI.Discovery.v1", json, StringComparison.Ordinal);
        Assert.Contains(options.Discovery.PublicWebSocketUrl, json, StringComparison.Ordinal);
        Assert.DoesNotContain(options.AuthenticationToken, json, StringComparison.Ordinal);
        Assert.DoesNotContain(options.LocalAuthenticationToken, json, StringComparison.Ordinal);
        Assert.DoesNotContain(options.PipeName, json, StringComparison.Ordinal);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pipe", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DiscoveryPayloadCanBeParsedByRemoteClient()
    {
        var options = ValidOptions();
        options.Discovery.Enabled = true;
        var broadcaster = new UdpDiscoveryBroadcaster(
            options,
            NullLogger<UdpDiscoveryBroadcaster>.Instance);

        var payload = broadcaster.CreatePayload();

        Assert.True(
            AgenticGatewayDiscovery.TryParseAnnouncement(payload, out var announcement));
        Assert.Equal(options.Discovery.PublicWebSocketUrl, announcement.WebSocketUrl);
    }

    [Fact]
    public void DiscoveryDestinationsIncludeLoopbackForLocalScanning()
    {
        var destinations = UdpDiscoveryBroadcaster.GetDiscoveryDestinations(47731).ToList();

        Assert.Contains(destinations, item => item.Address.Equals(IPAddress.Loopback));
        Assert.Contains(destinations, item => item.Address.Equals(IPAddress.Broadcast));
    }

    [Fact]
    public void RateLimiterRejectsRequestsPastTheConfiguredWindowLimit()
    {
        var limiter = new FixedWindowRateLimiter(2);
        var now = DateTimeOffset.UtcNow;

        Assert.True(limiter.TryAcquire(now));
        Assert.True(limiter.TryAcquire(now));
        Assert.False(limiter.TryAcquire(now));
        Assert.True(limiter.TryAcquire(now.AddMinutes(1)));
    }

    private static GatewayOptions ValidOptions() =>
        new()
        {
            AuthenticationToken = "gateway-token-0123456789-abcdefghijk",
            LocalAuthenticationToken = "local-pipe-token-0123456789-abcdefgh"
        };
}
