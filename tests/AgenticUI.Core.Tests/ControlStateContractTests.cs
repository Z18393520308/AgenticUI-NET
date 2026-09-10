using System.Text.Json;
using Xunit;

namespace AgenticUI.Core.Tests;

public sealed class ControlStateContractTests
{
    [Fact]
    public void SensitiveDescriptorKeepsOnlyBooleanReadOnlyConstraint()
    {
        var descriptor = new AgenticControlDescriptor { IsSensitive = true, State = new Dictionary<string, object?>
        { ["readOnly"] = true, ["text"] = "secret", ["path"] = "private/node", ["expanded"] = true } };
        var safe = AgenticPrivacy.SanitizeDescriptor(descriptor);
        Assert.Equal(true, safe.State["readOnly"]);
        Assert.Null(safe.State["text"]); Assert.Null(safe.State["path"]); Assert.Null(safe.State["expanded"]);
        Assert.Equal("secret", descriptor.State["text"]);
        var wire = JsonSerializer.Deserialize<AgenticControlDescriptor>(JsonSerializer.Serialize(descriptor, AgenticJson.Options), AgenticJson.Options)!;
        Assert.Equal(true, AgenticPrivacy.SanitizeDescriptor(wire).State["readOnly"]);
    }

    [Theory]
    [InlineData("private-content")]
    [InlineData("true")]
    public void SensitiveReadOnlyFieldCannotSmuggleStringData(string value)
    {
        var safe = AgenticPrivacy.SanitizeDescriptor(new AgenticControlDescriptor
        { IsSensitive = true, State = new Dictionary<string, object?> { ["readOnly"] = value } });
        Assert.Null(safe.State["readOnly"]);
    }

    [Fact]
    public async Task TreeSemanticRecordingUsesFullPathAndSkipsUnknownAndSensitivePaths()
    {
        var directory = Directory.CreateTempSubdirectory("aui-tree-contract-");
        var file = Path.Combine(directory.FullName, "events.jsonl");
        try
        {
            var bus = new AgenticEventBus();
            using (var recorder = new AgenticInteractionRecorder(file, bus, new AgenticControlRegistry()))
            {
                foreach (var name in new[] { AgenticEvents.SelectionChanged, AgenticEvents.Expanded, AgenticEvents.Collapsed })
                    await bus.PublishAsync(bus.Create("tree", name, AgenticEventSource.User,
                        new Dictionary<string, object?> { ["path"] = "公司/研发" }));
                await bus.PublishAsync(bus.Create("tree", AgenticEvents.SelectionChanged, AgenticEventSource.User,
                    new Dictionary<string, object?> { ["path"] = null }));
                var sensitive = bus.Create("tree", AgenticEvents.Expanded, AgenticEventSource.User,
                    new Dictionary<string, object?> { ["path"] = "secret" });
                sensitive.IsSensitive = true; await bus.PublishAsync(sensitive);
            }
            var commands = File.ReadAllLines(file).Select(line => JsonSerializer.Deserialize<AgenticCommand>(line, AgenticJson.Options)!).ToArray();
            Assert.Equal(new[] { "selectItem", "expand", "collapse" }, commands.Select(command => command.Action));
            Assert.All(commands, command => Assert.Equal("公司/研发", command.Arguments["path"]!.ToString()));
        }
        finally { directory.Delete(true); }
    }
}
