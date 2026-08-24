using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgenticUI;

public static class AgenticJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public enum AgenticEventSource
{
    User,
    Remote,
    Programmatic,
    Replay
}

public enum AgenticLogLevel
{
    Semantic,
    Detailed
}

public static class AgenticActions
{
    public const string Click = "click";
    public const string Focus = "focus";
    public const string Highlight = "highlight";
    public const string ClearHighlight = "clearHighlight";
    public const string SetText = "setText";
    public const string GetText = "getText";
    public const string SetValue = "setValue";
    public const string GetValue = "getValue";
    public const string SetChecked = "setChecked";
    public const string GetChecked = "getChecked";
    public const string SelectItem = "selectItem";
    public const string SelectRow = "selectRow";
    public const string GetRow = "getRow";
    public const string GetRows = "getRows";
    public const string GetColumns = "getColumns";
    public const string GetCell = "getCell";
    public const string SetCell = "setCell";
    public const string ScrollToRow = "scrollToRow";
    public const string AddRow = "addRow";
    public const string DeleteRow = "deleteRow";
    public const string SortByColumn = "sortByColumn";
    public const string FilterByColumn = "filterByColumn";
    public const string HighlightCell = "highlightCell";
    public const string SelectCell = "selectCell";
    public const string Expand = "expand";
    public const string Collapse = "collapse";
    public const string OpenDropDown = "openDropDown";
    public const string CloseDropDown = "closeDropDown";
    public const string MouseMove = "mouseMove";
    public const string MouseClick = "mouseClick";
    public const string MouseDoubleClick = "mouseDoubleClick";
    public const string MouseWheel = "mouseWheel";
    public const string MouseDrag = "mouseDrag";
}

public static class AgenticEvents
{
    public const string Clicked = "clicked";
    public const string Pressed = "pressed";
    public const string Released = "released";
    public const string TextChanged = "textChanged";
    public const string ValueChanged = "valueChanged";
    public const string CheckedChanged = "checkedChanged";
    public const string SelectionChanged = "selectionChanged";
    public const string Expanded = "expanded";
    public const string Collapsed = "collapsed";
    public const string DropDownOpened = "dropDownOpened";
    public const string DropDownClosed = "dropDownClosed";
    public const string FocusChanged = "focusChanged";
    public const string RemoteActionCompleted = "remoteActionCompleted";
    public const string RemoteActionRejected = "remoteActionRejected";
}

public sealed class AgenticControlDescriptor
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public bool IsTemporaryId { get; set; }
    public bool IsSensitive { get; set; }
    public bool IsEnabled { get; set; } = true;
    public IReadOnlyList<string> Actions { get; set; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, object?> State { get; set; } =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());
}

public sealed class AgenticEvent
{
    public long Sequence { get; set; }
    public string ControlId { get; set; } = "";
    public string Name { get; set; } = "";
    public AgenticEventSource Source { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public IReadOnlyDictionary<string, object?> Data { get; set; } =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());
}

public sealed class AgenticCommand
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
    public string ControlId { get; set; } = "";
    public string Action { get; set; } = "";
    public Dictionary<string, object?> Arguments { get; set; } = new();
}

public sealed class AgenticCommandResult
{
    public string RequestId { get; set; } = "";
    public bool Succeeded { get; set; }
    public string? Error { get; set; }
    public AgenticControlDescriptor? Control { get; set; }

    public static AgenticCommandResult Success(string requestId, AgenticControlDescriptor? control = null) =>
        new() { RequestId = requestId, Succeeded = true, Control = control };

    public static AgenticCommandResult Failure(string requestId, string error) =>
        new() { RequestId = requestId, Succeeded = false, Error = error };
}

public sealed class AgenticEnvelope
{
    public string Type { get; set; } = "";
    public object? Payload { get; set; }
}

[JsonSerializable(typeof(AgenticControlDescriptor))]
[JsonSerializable(typeof(AgenticEvent))]
[JsonSerializable(typeof(AgenticCommand))]
[JsonSerializable(typeof(AgenticCommandResult))]
[JsonSerializable(typeof(AgenticEnvelope))]
internal partial class AgenticJsonContext : JsonSerializerContext;
