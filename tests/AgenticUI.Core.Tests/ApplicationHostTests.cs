using AgenticUI.Remote;
using Xunit;

namespace AgenticUI.Core.Tests;

public sealed class ApplicationHostTests
{
    [Theory]
    [InlineData("{\"AgenticUI\":{\"AutoStart\":false}}", false)]
    [InlineData("{\"AgenticUI\":{\"AutoStrat\":false}}", true)]
    [InlineData("{}", true)]
    public void ConfigurationIsReadAndUnknownFieldsFailClosed(string json, bool invalid)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, json);
            using var host = AgenticApplicationHost.StartFromConfiguration(path);
            Assert.False(host.LocalRunning);
            Assert.Equal(invalid, host.LastError is not null);
        }
        finally { File.Delete(path); }
    }

    private static AgenticHostOptions Options() => new()
    {
        Local = new() { PipeName = "host-" + Guid.NewGuid().ToString("N"),
            TokenEnvironmentVariable = "AUI_TEST_" + Guid.NewGuid().ToString("N") }
    };

    [Fact]
    public async Task DefaultHostAuthenticatesAndIsSingletonUntilDisposed()
    {
        var options = Options();
        using (var host = AgenticApplicationHost.Start(options))
        {
            Assert.Null(host.LastError);
            Assert.True(host.LocalRunning);
            Assert.False(host.NetworkRunning);
            Assert.False(host.DiscoveryRunning);
            Assert.Same(host, AgenticApplicationHost.Start(Options()));
            var originalName = host.PipeName;
            options.Local.PipeName = "changed";
            Assert.Equal(originalName, host.PipeName);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var client = await AgenticNamedPipeClient.ConnectAsync(host.LocalAuthenticationToken,
                host.PipeName, cancellationToken: timeout.Token);
            Assert.Equal(RemoteMessageTypes.Controls, (await client.ListControlsAsync(timeout.Token)).Type);
        }
        Assert.Null(AgenticApplicationHost.Current);
        using var restarted = AgenticApplicationHost.Start(Options());
        Assert.True(restarted.LocalRunning);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void DisabledHostDoesNotListen(bool autoStart, bool local)
    {
        var options = Options(); options.AutoStart = autoStart; options.Local.Enabled = local;
        using var host = AgenticApplicationHost.Start(options);
        Assert.False(host.LocalRunning); Assert.False(host.NetworkRunning); Assert.Null(host.LastError);
    }

    [Fact]
    public void DiscoveryCannotImplicitlyEnableNetwork()
    {
        var options = Options(); options.Discovery.Enabled = true;
        using var host = AgenticApplicationHost.Start(options);
        Assert.True(host.LocalRunning); Assert.False(host.DiscoveryRunning);
    }

    [Fact]
    public void MissingNetworkTokenKeepsLocalAvailable()
    {
        var options = Options(); options.Network.Enabled = true;
        options.Network.TokenEnvironmentVariable = "AUI_TEST_" + Guid.NewGuid().ToString("N");
        using var host = AgenticApplicationHost.Start(options);
        Assert.True(host.LocalRunning); Assert.False(host.NetworkRunning);
        Assert.False(host.DiscoveryRunning); Assert.NotNull(host.LastError);
    }

    [Fact]
    public void InsecureNetworkConfigurationFailsClosedWithoutThrowing()
    {
        var options = Options(); options.Network.Enabled = true; options.Network.ListenUrl = "http://localhost:7443";
        using var host = AgenticApplicationHost.Start(options);
        Assert.False(host.LocalRunning); Assert.NotNull(host.LastError);
    }

    [Fact]
    public void MissingConfigurationDoesNotCrashBusinessApplication()
    {
        using var host = AgenticApplicationHost.StartFromConfiguration(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"));
        Assert.False(host.LocalRunning); Assert.NotNull(host.LastError);
    }
}
