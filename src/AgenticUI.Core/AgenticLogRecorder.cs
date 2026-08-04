using System.Text.Json;

namespace AgenticUI;

public sealed class AgenticLogOptions
{
    public AgenticLogLevel Level { get; set; } = AgenticLogLevel.Semantic;
    public bool RedactSensitiveValues { get; set; } = true;
    public string RedactedText { get; set; } = "***";
}

public sealed class AgenticLogRecorder : IAgenticEventSink, IDisposable
{
    private static readonly HashSet<string> DetailedEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        AgenticEvents.Pressed,
        AgenticEvents.Released,
        AgenticEvents.FocusChanged
    };

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly StreamWriter _writer;
    private readonly IDisposable _subscription;
    private readonly AgenticControlRegistry _registry;
    private readonly AgenticLogOptions _options;
    private bool _disposed;

    public AgenticLogRecorder(
        string filePath,
        AgenticEventBus? eventBus = null,
        AgenticControlRegistry? registry = null,
        AgenticLogOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A log file path is required.", nameof(filePath));
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
        _writer = new StreamWriter(
            new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read),
            new System.Text.UTF8Encoding(false))
        {
            AutoFlush = true
        };
        _registry = registry ?? AgenticControlRegistry.Default;
        _options = options ?? new AgenticLogOptions();
        _subscription = (eventBus ?? AgenticEventBus.Default).Subscribe(message => WriteAsync(message));
    }

    public async ValueTask WriteAsync(AgenticEvent message, CancellationToken cancellationToken = default)
    {
        if (_disposed || (_options.Level == AgenticLogLevel.Semantic && DetailedEvents.Contains(message.Name)))
        {
            return;
        }

        var sanitized = Sanitize(message);
        var json = JsonSerializer.Serialize(sanitized, AgenticJson.Options);
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _writer.WriteLineAsync(json).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
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

    private AgenticEvent Sanitize(AgenticEvent message)
    {
        if (!_options.RedactSensitiveValues ||
            !_registry.TryGet(message.ControlId, out var control) ||
            control?.Describe().IsSensitive != true)
        {
            return message;
        }

        var data = message.Data.ToDictionary(
            item => item.Key,
            item => IsPotentialValue(item.Key) ? _options.RedactedText : item.Value);
        return new AgenticEvent
        {
            Sequence = message.Sequence,
            ControlId = message.ControlId,
            Name = message.Name,
            Source = message.Source,
            Timestamp = message.Timestamp,
            Data = data
        };
    }

    private static bool IsPotentialValue(string key) =>
        key.IndexOf("text", StringComparison.OrdinalIgnoreCase) >= 0 ||
        key.IndexOf("value", StringComparison.OrdinalIgnoreCase) >= 0 ||
        key.IndexOf("selection", StringComparison.OrdinalIgnoreCase) >= 0;
}
