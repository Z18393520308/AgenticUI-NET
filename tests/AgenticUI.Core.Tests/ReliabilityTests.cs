using System.Globalization;
using System.Text.Json;
using AgenticUI;
using AgenticUI.Remote;
using Xunit;

namespace AgenticUI.Core.Tests;

public sealed class ReliabilityTests
{
    [Fact]
    public async Task NamedPipe_EventBurstDoesNotDisconnectAndReportsDroppedEvents()
    {
        var registry = new AgenticControlRegistry();
        var bus = new AgenticEventBus();
        registry.Register(new StaticControl("burst.target"), "burst.target");
        var pipeName = $"aui-{Guid.NewGuid():N}";
        using var server = new AgenticNamedPipeServer(pipeName, registry, new AgenticCommandDispatcher(registry, bus), bus);
        server.Start();
        using var client = await AgenticNamedPipeClient.ConnectAsync(server.AuthenticationToken, pipeName);
        var dropped = new TaskCompletionSource<AgenticEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = 0;
        client.EventReceived += message =>
        {
            if (message.Name == AgenticEvents.EventsDropped) dropped.TrySetResult(message);
            else Interlocked.Increment(ref received);
        };

        // 同步突发远超队列上限（例如日志文本框连续追加），旧实现会直接断开连接。
        var data = new Dictionary<string, object?> { ["text"] = new string('x', 200) };
        for (var index = 0; index < 5000; index++)
            _ = bus.PublishAsync("burst.target", AgenticEvents.TextChanged, AgenticEventSource.User, data);

        var notice = await dropped.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(((JsonElement)notice.Data["dropped"]!).GetInt32() > 0);
        Assert.Equal(string.Empty, notice.ControlId);
        Assert.True(client.IsConnected);
        Assert.True(received > 0);
        Assert.Equal(RemoteMessageTypes.Controls, (await client.ListControlsAsync()).Type);
    }

    [Fact]
    public void NamedPipe_StartReportsPipeCreationFailure()
    {
        // "anonymous" 是保留的管道名，任何平台都无法创建；旧实现会吞掉错误并报告 IsRunning=true。
        using var server = new AgenticNamedPipeServer("anonymous");
        Assert.ThrowsAny<Exception>(() => server.Start());
        Assert.False(server.IsRunning);
    }

    [Fact]
    public void Registry_SnapshotSkipsControlsThatFailToDescribe()
    {
        var registry = new AgenticControlRegistry();
        registry.Register(new StaticControl("ok"), "ok");
        registry.Register(new ThrowingControl(), "closing");
        var snapshot = registry.Snapshot(remotelyDiscoverableOnly: false);
        Assert.Equal("ok", Assert.Single(snapshot).Id);
    }

    [Fact]
    public void Arguments_ParseNumbersAndDatesIndependentOfCurrentCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var json = JsonDocument.Parse("""{"number":1.5,"text":"1.5","integer":7,"date":"2026-10-16"}""").RootElement;
            Assert.Equal(1.5, AgenticArguments.ReadDouble(json.GetProperty("number"), "value"));
            Assert.Equal(1.5, AgenticArguments.ReadDouble(json.GetProperty("text"), "value"));
            Assert.Equal(1.5m, AgenticArguments.ReadDecimal(json.GetProperty("number"), "value"));
            Assert.Equal(1.5m, AgenticArguments.ReadDecimal("1.5", "value"));
            Assert.Equal(7, AgenticArguments.ReadInt32(json.GetProperty("integer"), "value"));
            Assert.Equal(new DateTime(2026, 10, 16), AgenticArguments.ReadDateTime(json.GetProperty("date"), "value"));
            Assert.Throws<ArgumentException>(() => AgenticArguments.ReadInt32(json.GetProperty("number"), "value"));
            Assert.Throws<ArgumentException>(() => AgenticArguments.ReadDouble(null, "value"));
            Assert.Throws<ArgumentException>(() => AgenticArguments.ReadDouble("NaN", "value"));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    private sealed class StaticControl(string id) : IAgenticControl
    {
        public AgenticControlDescriptor Describe() => new() { Id = id, Name = id, Kind = "test" };
        public Task<AgenticCommandResult> ExecuteAsync(AgenticCommand command, CancellationToken cancellationToken = default) =>
            Task.FromResult(AgenticCommandResult.Success(command.RequestId, Describe()));
        public bool IsRemotelyDiscoverable() => true;
    }

    private sealed class ThrowingControl : IAgenticControl
    {
        public AgenticControlDescriptor Describe() => throw new InvalidOperationException("window is closing");
        public Task<AgenticCommandResult> ExecuteAsync(AgenticCommand command, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();
        public bool IsRemotelyDiscoverable() => true;
    }
}
