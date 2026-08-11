using System.Text.Json;
using AgenticUI;
using AgenticUI.Remote;
using Xunit;

namespace AgenticUI.Core.Tests;

public sealed class CoreTests
{
    [Fact]
    public void Registry_UsesStableAndTemporaryIds()
    {
        var registry = new AgenticControlRegistry();
        var stable = new FakeControl("save", false);
        var temporary = new FakeControl("", false);

        Assert.Equal("account.save", registry.Register(stable, "account.save"));
        Assert.StartsWith("temporary.", registry.Register(temporary));
        Assert.Equal(2, registry.Snapshot().Count);
    }

    [Fact]
    public async Task Registry_SnapshotDoesNotHoldLockWhileDescribingControls()
    {
        var registry = new AgenticControlRegistry();
        var control = new CallbackControl(
            "first",
            () =>
            {
                var registration = Task.Run(
                    () => registry.Register(new FakeControl("second", false), "second"));
                if (!registration.Wait(TimeSpan.FromSeconds(2)))
                {
                    throw new TimeoutException("Register was blocked by Snapshot while Describe was running.");
                }
            });
        registry.Register(control, "first");

        var snapshot = await Task.Run(() => registry.Snapshot()).WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Contains(snapshot, descriptor => descriptor.Id == "first");
    }

    [Fact]
    public async Task EventBus_AssignsMonotonicSequenceNumbers()
    {
        var bus = new AgenticEventBus();
        var messages = new List<AgenticEvent>();
        using var subscription = bus.Subscribe(message =>
        {
            messages.Add(message);
            return ValueTask.CompletedTask;
        });

        await bus.PublishAsync("one", AgenticEvents.Clicked);
        await bus.PublishAsync("two", AgenticEvents.TextChanged);

        Assert.Equal(2, messages.Count);
        Assert.True(messages[1].Sequence > messages[0].Sequence);
    }

    [Fact]
    public async Task Dispatcher_ExecutesSupportedSemanticAction()
    {
        var registry = new AgenticControlRegistry();
        var control = new FakeControl("save", false);
        registry.Register(control, "account.save");
        var dispatcher = new AgenticCommandDispatcher(registry, new AgenticEventBus());

        var result = await dispatcher.DispatchAsync(new AgenticCommand
        {
            ControlId = "account.save",
            Action = AgenticActions.Click
        });

        Assert.True(result.Succeeded);
        Assert.Equal(1, control.ExecutionCount);
    }

    [Fact]
    public async Task Recorder_RedactsSensitiveTextByDefault()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"agenticui-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "events.jsonl");
        var registry = new AgenticControlRegistry();
        var bus = new AgenticEventBus();
        var control = new FakeControl("password", true);
        registry.Register(control, "login.password");

        using (var recorder = new AgenticLogRecorder(file, bus, registry))
        {
            await bus.PublishAsync(
                "login.password",
                AgenticEvents.TextChanged,
                data: new Dictionary<string, object?> { ["text"] = "secret" });
        }

        var document = JsonDocument.Parse(await File.ReadAllTextAsync(file));
        Assert.Equal("***", document.RootElement.GetProperty("data").GetProperty("text").GetString());
        Directory.Delete(directory, true);
    }

    [Fact]
    public async Task InteractionRecorder_CreatesReplayableSemanticCommands()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"agenticui-recording-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "recording.jsonl");
        var bus = new AgenticEventBus();

        using (var recorder = new AgenticInteractionRecorder(file, bus))
        {
            await bus.PublishAsync("login.submit", AgenticEvents.Clicked);
        }

        var commands = await AgenticReplay.LoadCommandsAsync(file);
        Assert.Single(commands);
        Assert.Equal("login.submit", commands[0].ControlId);
        Assert.Equal(AgenticActions.Click, commands[0].Action);
        Directory.Delete(directory, true);
    }

    [Fact]
    public async Task NamedPipeGateway_ListsAndExecutesRegisteredControls()
    {
        var registry = new AgenticControlRegistry();
        var bus = new AgenticEventBus();
        var control = new FakeControl("remote.button", false);
        registry.Register(control, "remote.button");
        var pipeName = $"aui-{Guid.NewGuid():N}";
        using var server = new AgenticNamedPipeServer(
            pipeName,
            registry,
            new AgenticCommandDispatcher(registry, bus),
            bus);
        server.Start();
        using var client = await AgenticNamedPipeClient.ConnectAsync(
            server.AuthenticationToken,
            pipeName,
            "integration-test");

        var listed = await client.ListControlsAsync();
        var executed = await client.ExecuteAsync(new AgenticCommand
        {
            ControlId = "remote.button",
            Action = AgenticActions.Click
        });

        Assert.Single(listed.Controls!);
        Assert.True(executed.Result!.Succeeded);
        Assert.Equal(1, control.ExecutionCount);
    }

    [Fact]
    public async Task NamedPipeGateway_RejectsInvalidAuthenticationToken()
    {
        var pipeName = ShortPipeName();
        using var server = new AgenticNamedPipeServer(pipeName);
        server.Start();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => AgenticNamedPipeClient.ConnectAsync("incorrect-token-value", pipeName));
    }

    [Fact]
    public async Task NamedPipeClient_CorrelatesConcurrentRequests()
    {
        var registry = new AgenticControlRegistry();
        registry.Register(new FakeControl("remote.concurrent", false), "remote.concurrent");
        var pipeName = ShortPipeName();
        using var server = new AgenticNamedPipeServer(
            pipeName,
            registry,
            new AgenticCommandDispatcher(registry));
        server.Start();
        using var client = await AgenticNamedPipeClient.ConnectAsync(server.AuthenticationToken, pipeName);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 12).Select(_ => client.ListControlsAsync()));

        Assert.All(responses, response => Assert.Single(response.Controls!));
        Assert.Equal(12, responses.Select(response => response.RequestId).Distinct().Count());
    }

    [Fact]
    public async Task NamedPipeClient_ReceivesBroadcastEventsWhileIdle()
    {
        var bus = new AgenticEventBus();
        var pipeName = ShortPipeName();
        using var server = new AgenticNamedPipeServer(pipeName, events: bus);
        server.Start();
        using var client = await AgenticNamedPipeClient.ConnectAsync(server.AuthenticationToken, pipeName);
        var received = new TaskCompletionSource<AgenticEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.EventReceived += message => received.TrySetResult(message);

        await bus.PublishAsync("dashboard.refresh", AgenticEvents.Clicked);
        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Equal("dashboard.refresh", message.ControlId);
        Assert.Equal(AgenticEvents.Clicked, message.Name);
    }

    [Fact]
    public async Task NamedPipeClient_CanBeDisposedFromEventCallback()
    {
        var bus = new AgenticEventBus();
        var pipeName = ShortPipeName();
        using var server = new AgenticNamedPipeServer(pipeName, events: bus);
        server.Start();
        var client = await AgenticNamedPipeClient.ConnectAsync(server.AuthenticationToken, pipeName);
        var disposed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.EventReceived += _ =>
        {
            client.Dispose();
            disposed.TrySetResult(true);
        };

        await bus.PublishAsync("dashboard.close", AgenticEvents.Clicked);
        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.False(client.IsConnected);
        client.Dispose();
    }

    private static string ShortPipeName() => $"aui-{Guid.NewGuid():N}";

    private sealed class FakeControl : IAgenticControl
    {
        private readonly string _name;
        private readonly bool _sensitive;

        public FakeControl(string name, bool sensitive)
        {
            _name = name;
            _sensitive = sensitive;
        }

        public int ExecutionCount { get; private set; }

        public AgenticControlDescriptor Describe() => new()
        {
            Id = _name,
            Name = _name,
            Kind = "button",
            IsSensitive = _sensitive,
            Actions = new[] { AgenticActions.Click }
        };

        public Task<AgenticCommandResult> ExecuteAsync(
            AgenticCommand command,
            CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            return Task.FromResult(AgenticCommandResult.Success(command.RequestId, Describe()));
        }

        public bool IsRemotelyDiscoverable() => true;
    }

    private sealed class CallbackControl : IAgenticControl
    {
        private readonly string _id;
        private readonly Action _onDescribe;

        public CallbackControl(string id, Action onDescribe)
        {
            _id = id;
            _onDescribe = onDescribe;
        }

        public AgenticControlDescriptor Describe()
        {
            _onDescribe();
            return new AgenticControlDescriptor
            {
                Id = _id,
                Name = _id,
                Kind = "test"
            };
        }

        public Task<AgenticCommandResult> ExecuteAsync(
            AgenticCommand command,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(AgenticCommandResult.Success(command.RequestId, Describe()));

        public bool IsRemotelyDiscoverable() => true;
    }
}
