using System.Text.Json;
using AgenticUI.Remote;
using Xunit;

namespace AgenticUI.Core.Tests;

public sealed class GuidanceTests
{
    [Fact]
    public void RemoteTextOverridesDefaultsAndExplicitFalseNeverFallsBack()
    {
        var command = new AgenticCommand { Arguments = { ["hint"] = "远程内容", ["instructionNumber"] = 2 } };
        var options = AgenticGuidanceOptions.FromCommand(command, 1, "默认内容");
        Assert.Equal("远程内容", options.Hint);
        Assert.Equal(2, options.InstructionNumber);
        command.Arguments["showBubble"] = false;
        options = AgenticGuidanceOptions.FromCommand(command, 1, "默认内容");
        Assert.False(options.ShowBubble);
        Assert.Null(options.Hint);
        Assert.True(options.ShowOutline);
        Assert.Equal("默认内容", AgenticGuidanceOptions.FromCommand(new AgenticCommand(), 0, "默认内容").Hint);
    }

    [Theory]
    [InlineData("{\"showBubble\":true}")]
    [InlineData("{\"showBubble\":\"false\"}")]
    [InlineData("{\"durationMs\":-1}")]
    [InlineData("{\"placement\":\"script\"}")]
    [InlineData("{\"guidanceId\":\"\"}")]
    [InlineData("{\"showNumber\":true,\"instructionNumber\":0}")]
    public void InvalidPresentationIsRejected(string json)
    {
        var command = new AgenticCommand { Arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(json)! };
        Assert.Throws<ArgumentException>(() => AgenticGuidanceOptions.FromCommand(command));
    }

    [Fact]
    public void ClientCannotSerializeOrSupplySessionIdentity()
    {
        var command = JsonSerializer.Deserialize<AgenticCommand>("{\"sessionId\":\"spoof\"}", AgenticJson.Options)!;
        Assert.Null(command.SessionId);
        command.SessionId = "server-session";
        Assert.DoesNotContain("server-session", JsonSerializer.Serialize(command, AgenticJson.Options));
    }

    [Fact]
    public void UpdateReusesVisualAndClearIsIsolatedBySessionAndGuidanceId()
    {
        using var visuals = new AgenticGuidanceCollection();
        var first = new Visual(); var second = new Visual();
        var command = new AgenticCommand { SessionId = "first" };
        visuals.Show(command, new AgenticGuidanceOptions(), () => first);
        visuals.Show(command, new AgenticGuidanceOptions { ShowBubble = true, Hint = "更新" },
            () => throw new InvalidOperationException("不能重新创建"));
        Assert.Equal(2, first.Updates);
        command.SessionId = "second";
        visuals.Show(command, new AgenticGuidanceOptions(), () => second);
        visuals.Clear("first");
        Assert.True(first.IsDisposed);
        Assert.False(second.IsDisposed);
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(1900, 1040, 1.5)]
    [InlineData(-1800, 900, 2)]
    public void BubbleStaysInsideTargetMonitor(double x, double y, double scale)
    {
        var area = new GuidanceRect(x < 0 ? -1920 : 0, 0, 1920, 1080);
        var layout = AgenticGuidanceLayout.Calculate(new GuidanceRect(x, y, 100, 30), area,
            340 * scale, 150 * scale, scale, new AgenticGuidanceOptions { ShowBubble = true, Hint = "提示", ShowNumber = true });
        Assert.True(layout.Bounds.X >= area.X && layout.Bounds.Y >= area.Y);
        Assert.True(layout.Bounds.Right <= area.Right && layout.Bounds.Bottom <= area.Bottom);
    }

    [Fact]
    public async Task SubscriberFailureDoesNotTurnExecutedCommandIntoFailureOrDropFollowingSubscribers()
    {
        var bus = new AgenticEventBus(); var registry = new AgenticControlRegistry();
        var target = new Target(); registry.Register(target, "target");
        var observed = 0; var faults = 0;
        bus.SubscriberFaulted += _ => faults++;
        using var throwing = bus.Subscribe(_ => throw new IOException("log unavailable"));
        using var observer = bus.Subscribe(_ => { observed++; return default; });
        var result = await new AgenticCommandDispatcher(registry, bus).DispatchAsync(new AgenticCommand { ControlId = "target", Action = "click" });
        Assert.True(result.Succeeded); Assert.Equal(1, target.Executions);
        Assert.Equal(1, observed); Assert.Equal(1, faults);
    }

    [Fact]
    public async Task AllRejectedRequestsHaveCorrelatedAuditEvents()
    {
        var bus = new AgenticEventBus(); var registry = new AgenticControlRegistry();
        var target = new Target(); registry.Register(target, "target");
        var events = new List<AgenticEvent>();
        using var subscription = bus.Subscribe(e => { events.Add(e); return default; });
        var dispatcher = new AgenticCommandDispatcher(registry, bus);
        await dispatcher.DispatchAsync(new AgenticCommand { ControlId = "missing", Action = "click", RequestId = "one" });
        await dispatcher.DispatchAsync(new AgenticCommand { ControlId = "target", Action = "unknown", RequestId = "two" });
        Assert.Equal(new[] { "one", "two" }, events.Select(e => e.Data["requestId"]));
        Assert.All(events, e => Assert.Equal(AgenticEvents.RemoteActionRejected, e.Name));
    }

    [Fact]
    public async Task PipeCleansOnlyDisconnectedSessionAndRedactsSensitiveEvents()
    {
        var bus = new AgenticEventBus(); var registry = new AgenticControlRegistry();
        var target = new Target(); registry.Register(target, "target");
        var pipe = "aui-" + Guid.NewGuid().ToString("N");
        using var server = new AgenticNamedPipeServer(pipe, registry, events: bus); server.Start();
        using var first = await AgenticNamedPipeClient.ConnectAsync(server.AuthenticationToken, pipe);
        using var second = await AgenticNamedPipeClient.ConnectAsync(server.AuthenticationToken, pipe);
        await first.ExecuteAsync(new AgenticCommand { ControlId = "target", Action = "click", SessionId = "spoof" });
        var session = target.LastSession;
        await second.ExecuteAsync(new AgenticCommand { ControlId = "target", Action = "click" });
        Assert.NotEqual("spoof", session); Assert.NotEqual(session, target.LastSession);
        var received = new TaskCompletionSource<AgenticEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        second.EventReceived += message => { if (message.Name == AgenticEvents.TextChanged) received.TrySetResult(message); };
        await bus.PublishAsync(new AgenticEvent { ControlId = "already-unloaded", Name = AgenticEvents.TextChanged,
            IsSensitive = true, Data = new Dictionary<string, object?> { ["text"] = "never-export-this" } });
        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.DoesNotContain("never-export-this", JsonSerializer.Serialize(message));
        first.Dispose();
        Assert.Equal(session, await target.Cleared.Task.WaitAsync(TimeSpan.FromSeconds(3)));
    }

    private sealed class Visual : IAgenticGuidanceVisual
    {
        public int Updates { get; private set; }
        public bool IsDisposed { get; private set; }
        public void Update(AgenticGuidanceOptions options) => Updates++;
        public void Dispose() => IsDisposed = true;
    }
    private sealed class Target : IAgenticControl, IAgenticGuidanceControl
    {
        public int Executions { get; private set; }
        public string? LastSession { get; private set; }
        public TaskCompletionSource<string> Cleared { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public AgenticControlDescriptor Describe() => new() { Id = "target", Actions = new[] { "click" } };
        public bool IsRemotelyDiscoverable() => true;
        public Task<AgenticCommandResult> ExecuteAsync(AgenticCommand command, CancellationToken cancellationToken = default)
        { Executions++; LastSession = command.SessionId; return Task.FromResult(AgenticCommandResult.Success(command.RequestId)); }
        public Task ClearGuidanceAsync(string sessionId) { Cleared.TrySetResult(sessionId); return Task.CompletedTask; }
    }
}
