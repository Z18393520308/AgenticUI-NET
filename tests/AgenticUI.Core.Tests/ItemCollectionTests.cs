using System.Text.Json;
using Xunit;

namespace AgenticUI.Core.Tests;

public sealed class ItemCollectionTests
{
    [Theory]
    [InlineData("三体", 0)]
    [InlineData("  三体 \t", 0)]
    [InlineData("EARTH", 1)]
    [InlineData("Legacy.Book", 0)]
    public void DisplayTextAndUnambiguousLegacyTextRemainSelectable(string value, int index)
    {
        var items = Create(new(new object(), "三体", "BK-001", legacyText: "Legacy.Book"),
            new(new object(), "earth", "BK-002"));
        Assert.Equal(index, items.ResolveIndex(Command(("value", value))));
    }

    [Fact]
    public void ExactTextWinsBeforeWhitespaceNormalizationAndNoSubstringGuessing()
    {
        var items = Create(new("first", "三体"), new("second", " 三体 "));
        Assert.Equal(0, items.ResolveIndex(Command(("value", "三体"))));
        Assert.Equal(1, items.ResolveIndex(Command(("value", " 三体 "))));
        Assert.Throws<InvalidOperationException>(() => items.ResolveIndex(Command(("value", "\t三体"))));
        Assert.Throws<ArgumentException>(() => Create(new AgenticItemEntry("book", "BK-001 三体"))
            .ResolveIndex(Command(("value", "三体"))));
    }

    [Fact]
    public void DuplicateLabelsAndKeysNeverSelectFirstMatch()
    {
        var items = Create(new("first", "三体", "1"), new("second", "三体", "2"));
        Assert.Throws<InvalidOperationException>(() => items.ResolveIndex(Command(("value", "三体"))));
        Assert.Equal(1, items.ResolveIndex(Command(("itemKey", "2"))));
        items.Update(new[] { new AgenticItemEntry("a", "A", "same"), new AgenticItemEntry("b", "B", "same") });
        Assert.Throws<InvalidOperationException>(() => items.ResolveIndex(Command(("itemKey", "same"))));
        Assert.Throws<ArgumentException>(() => items.ResolveIndex(Command(("itemKey", "SAME"))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public void DisabledOrUnknownAvailabilityCannotBeBypassedWithAnIndex(bool? enabled)
    {
        var items = Create(new AgenticItemEntry("book", "三体", "BK-001", enabled));
        foreach (var selector in new[] { ("index", (object?)0), ("value", (object?)"三体"), ("itemKey", (object?)"BK-001") })
            Assert.Throws<InvalidOperationException>(() => items.ResolveIndex(Command(selector)));
    }

    [Fact]
    public void SnapshotRejectsReorderingReplacementAndChangedLabelsButNotSelection()
    {
        var first = new AgenticItemEntry(new object(), "A", "1");
        var second = new AgenticItemEntry(new object(), "B", "2");
        var items = Create(first, second);
        var version = items.Version;
        items.Update(new[] { first, second });
        Assert.Equal(version, items.Version);
        items.ReadPage(Command(), 1);
        Assert.Equal(version, items.Version);
        items.Update(new[] { second, first });
        Assert.Throws<InvalidOperationException>(() => items.ResolveIndex(Command(("index", 0), ("itemsVersion", version))));
        Assert.Throws<InvalidOperationException>(() => items.ReadPage(Command(("itemsVersion", version)), 0));
        Assert.Equal(1, items.ResolveIndex(Command(("itemKey", "1"))));
        version = items.Version;
        items.Update(new[] { new AgenticItemEntry(new object(), "B", "2"), first });
        Assert.NotEqual(version, items.Version);
        version = items.Version;
        items.Update(new[] { new AgenticItemEntry(second.Source, "new label", "2"), first });
        Assert.NotEqual(version, items.Version);
    }

    [Fact]
    public void PageIsBoundedAndNeverSerializesBusinessObjectsOrCompatibilityText()
    {
        var items = Create(Enumerable.Range(0, 600).Select(i =>
            new AgenticItemEntry(new { Secret = "do-not-send" }, "Book " + i, i.ToString(), legacyText: "private-legacy")).ToArray());
        var page = JsonSerializer.SerializeToElement(items.ReadPage(Command(("start", 10), ("count", 2)), 11), AgenticJson.Options);
        Assert.Equal(600, page.GetProperty("total").GetInt32());
        Assert.Equal(2, page.GetProperty("count").GetInt32());
        Assert.Equal(10, page.GetProperty("items")[0].GetProperty("index").GetInt32());
        Assert.True(page.GetProperty("items")[1].GetProperty("isSelected").GetBoolean());
        Assert.DoesNotContain("do-not-send", page.ToString());
        Assert.DoesNotContain("private-legacy", page.ToString());
        Assert.Equal(50, items.ReadPage(Command(), -1)["count"]);
        Assert.Equal(0, items.ReadPage(Command(("start", 600)), -1)["count"]);
        Assert.True(AgenticActionPolicy.IsObservation(AgenticActions.GetItems));
    }

    [Theory]
    [InlineData("start", -1)]
    [InlineData("count", 0)]
    [InlineData("count", 501)]
    [InlineData("start", "bad")]
    public void InvalidPageArgumentsAreRejected(string name, object value) =>
        Assert.Throws<ArgumentException>(() => Create().ReadPage(Command((name, value)), -1));

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData("NaN")]
    public void InvalidIndexNeverFallsBackToValue(object index) =>
        Assert.ThrowsAny<ArgumentException>(() => Create(new AgenticItemEntry("book", "三体"))
            .ResolveIndex(Command(("index", index), ("value", "三体"))));

    [Fact]
    public void JsonArgumentsAndScalarKeysRoundTripWithoutSerializingObjects()
    {
        var command = JsonSerializer.Deserialize<AgenticCommand>(
            "{\"action\":\"selectItem\",\"arguments\":{\"itemKey\":\"42\"}}", AgenticJson.Options)!;
        var items = Create(new AgenticItemEntry("book", "三体", AgenticItemCollection.ScalarKey(42)));
        Assert.Equal(0, items.ResolveIndex(command));
        Assert.Null(AgenticItemCollection.ScalarKey(new { Secret = "private" }));
        Assert.Throws<ArgumentException>(() => items.ResolveIndex(Command(("itemKey", "42"), ("index", 0))));
        Assert.Throws<ArgumentException>(() => items.ResolveIndex(Command(("itemKey", 42))));
    }

    [Fact]
    public void SensitiveCandidateStateAndEventAreRedacted()
    {
        var page = Create(new AgenticItemEntry("private-object", "private-title", "private-id")).ReadPage(Command(), 0);
        var sanitized = AgenticPrivacy.SanitizeDescriptor(new AgenticControlDescriptor { IsSensitive = true, State = page });
        Assert.All(sanitized.State.Values, value => Assert.Null(value));
        var message = AgenticPrivacy.SanitizeEvent(new AgenticEvent
        { IsSensitive = true, Data = new Dictionary<string, object?> { ["selection"] = "private-title", ["itemKey"] = "private-id" } }, true);
        Assert.All(message.Data.Values, value => Assert.Equal("***", value));
    }

    [Fact]
    public async Task RecorderPrefersItemKeyAndRetainsLegacyIndexFallback()
    {
        var directory = Directory.CreateTempSubdirectory("aui-item-recording-");
        try
        {
            var file = Path.Combine(directory.FullName, "events.jsonl");
            var bus = new AgenticEventBus();
            using (var recorder = new AgenticInteractionRecorder(file, bus, new AgenticControlRegistry()))
            {
                await bus.PublishAsync(bus.Create("books", AgenticEvents.SelectionChanged, AgenticEventSource.User,
                    new Dictionary<string, object?> { ["index"] = 2, ["itemKey"] = "BK-001" }));
                await bus.PublishAsync(bus.Create("legacy", AgenticEvents.SelectionChanged, AgenticEventSource.User,
                    new Dictionary<string, object?> { ["index"] = 1 }));
                var secret = bus.Create("secret", AgenticEvents.SelectionChanged, AgenticEventSource.User,
                    new Dictionary<string, object?> { ["index"] = 1, ["itemKey"] = "private-key" });
                secret.IsSensitive = true;
                await bus.PublishAsync(secret);
            }
            var commands = File.ReadAllLines(file).Select(line => JsonSerializer.Deserialize<AgenticCommand>(line, AgenticJson.Options)!).ToArray();
            Assert.Equal(2, commands.Length);
            Assert.Equal("BK-001", commands[0].Arguments["itemKey"]!.ToString());
            Assert.False(commands[0].Arguments.ContainsKey("index"));
            Assert.Equal("1", commands[1].Arguments["index"]!.ToString());
        }
        finally { directory.Delete(true); }
    }

    private static AgenticItemCollection Create(params AgenticItemEntry[] items)
    {
        var collection = new AgenticItemCollection(); collection.Update(items); return collection;
    }
    private static AgenticCommand Command(params (string Key, object? Value)[] args) => new()
    { Arguments = args.ToDictionary(arg => arg.Key, arg => arg.Value) };
}
