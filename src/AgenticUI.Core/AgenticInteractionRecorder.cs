using System.Text.Json;

namespace AgenticUI;

public sealed class AgenticRecordingOptions
{
    public bool RecordSensitiveText { get; set; }
    public bool RecordProgrammaticEvents { get; set; }
}

public sealed class AgenticInteractionRecorder : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly AgenticControlRegistry _registry;
    private readonly AgenticRecordingOptions _options;
    private readonly IDisposable _subscription;
    private bool _disposed;

    public AgenticInteractionRecorder(
        string filePath,
        AgenticEventBus? eventBus = null,
        AgenticControlRegistry? registry = null,
        AgenticRecordingOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A recording file path is required.", nameof(filePath));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
        _writer = new StreamWriter(
            new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read),
            new System.Text.UTF8Encoding(false))
        {
            AutoFlush = true
        };
        _registry = registry ?? AgenticControlRegistry.Default;
        _options = options ?? new AgenticRecordingOptions();
        _subscription = (eventBus ?? AgenticEventBus.Default).Subscribe(RecordAsync);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _subscription.Dispose();
        _writer.Dispose();
        _writeLock.Dispose();
    }

    private async ValueTask RecordAsync(AgenticEvent message)
    {
        if (_disposed ||
            message.Source is AgenticEventSource.Remote or AgenticEventSource.Replay ||
            (!_options.RecordProgrammaticEvents && message.Source == AgenticEventSource.Programmatic))
        {
            return;
        }

        var command = ToCommand(message);
        if (command is null)
        {
            return;
        }

        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await _writer.WriteLineAsync(JsonSerializer.Serialize(command, AgenticJson.Options)).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private AgenticCommand? ToCommand(AgenticEvent message)
    {
        switch (message.Name)
        {
            case AgenticEvents.Clicked:
                return Command(message.ControlId, AgenticActions.Click);
            case AgenticEvents.TextChanged:
                if (_registry.TryGet(message.ControlId, out var textControl) &&
                    textControl?.Describe().IsSensitive == true &&
                    !_options.RecordSensitiveText)
                {
                    return null;
                }
                return Command(message.ControlId, AgenticActions.SetText, "text", Value(message, "text"));
            case AgenticEvents.CheckedChanged:
                return Command(
                    message.ControlId,
                    AgenticActions.SetChecked,
                    "checked",
                    Value(message, "checked"));
            case AgenticEvents.SelectionChanged:
                return Command(
                    message.ControlId,
                    AgenticActions.SelectItem,
                    "index",
                    Value(message, "index"));
            default:
                return null;
        }
    }

    private static object? Value(AgenticEvent message, string key) =>
        message.Data.TryGetValue(key, out var value) ? value : null;

    private static AgenticCommand Command(
        string controlId,
        string action,
        string? argument = null,
        object? value = null)
    {
        var command = new AgenticCommand { ControlId = controlId, Action = action };
        if (argument is not null)
        {
            command.Arguments[argument] = value;
        }
        return command;
    }
}
