using System.Globalization;
using System.Text.Json;

namespace AgenticUI;

/// <summary>跨 UI 框架的纯展示协议。没有模型调用，也不会执行气泡中的文本。</summary>
public sealed class AgenticGuidanceOptions
{
    public const string Capability = "dynamicGuidance.v1";
    public const int MaximumTextLength = 1000;
    public string GuidanceId { get; set; } = "default";
    public bool ShowOutline { get; set; } = true;
    public bool ShowNumber { get; set; }
    public int InstructionNumber { get; set; }
    public bool ShowBubble { get; set; }
    public string? Hint { get; set; }
    public string Placement { get; set; } = "auto";
    public int DurationMs { get; set; }

    /// <summary>每次命令都是完整展示快照，不继承上一次远程提示，防止残留文本。</summary>
    public static AgenticGuidanceOptions FromCommand(AgenticCommand command, int defaultNumber = 0, string? defaultHint = null)
    {
        var args = command.Arguments;
        var number = args.ContainsKey("instructionNumber")
            ? Integer(args, "instructionNumber", defaultNumber, 0, 99999)
            : Integer(args, "number", defaultNumber, 0, 99999);
        var hint = args.ContainsKey("hint") ? String(args, "hint", null) : defaultHint;
        var result = new AgenticGuidanceOptions
        {
            GuidanceId = ReadGuidanceId(command) ?? "default",
            ShowOutline = Boolean(args, "showOutline", true),
            ShowNumber = Boolean(args, "showNumber", number > 0),
            InstructionNumber = number,
            ShowBubble = Boolean(args, "showBubble", !string.IsNullOrWhiteSpace(hint)),
            Hint = hint,
            Placement = String(args, "placement", "auto") ?? "auto",
            DurationMs = Integer(args, "durationMs", 0, 0, 3600000)
        };
        if (result.Placement is not ("auto" or "top" or "bottom"))
            throw new ArgumentException("placement must be auto, top or bottom.");
        if (hint?.Length > MaximumTextLength)
            throw new ArgumentException($"hint must not exceed {MaximumTextLength} characters.");
        if (result.ShowBubble && string.IsNullOrWhiteSpace(hint))
            throw new ArgumentException("showBubble=true requires non-empty hint text.");
        if (result.ShowNumber && number == 0)
            throw new ArgumentException("showNumber=true requires a positive instructionNumber.");
        // 明确关闭时丢弃文本，不能回退到控件预设 Hint，也不把隐藏的正文送给渲染器。
        if (!result.ShowBubble) result.Hint = null;
        return result;
    }

    public static string? ReadGuidanceId(AgenticCommand command)
    {
        if (!command.Arguments.ContainsKey("guidanceId")) return null;
        var id = String(command.Arguments, "guidanceId", null);
        if (string.IsNullOrWhiteSpace(id) || id!.Length > 128 || id.Any(char.IsControl))
            throw new ArgumentException("guidanceId must contain 1-128 printable characters.");
        return id;
    }

    private static bool Boolean(Dictionary<string, object?> args, string key, bool fallback)
    {
        if (!args.TryGetValue(key, out var value)) return fallback;
        if (value is bool boolean) return boolean;
        if (value is JsonElement json && json.ValueKind is JsonValueKind.True or JsonValueKind.False) return json.GetBoolean();
        throw new ArgumentException($"{key} must be a boolean.");
    }

    private static string? String(Dictionary<string, object?> args, string key, string? fallback)
    {
        if (!args.TryGetValue(key, out var value)) return fallback;
        if (value is null) return null;
        if (value is string text) return text;
        if (value is JsonElement json && json.ValueKind is JsonValueKind.String or JsonValueKind.Null) return json.GetString();
        throw new ArgumentException($"{key} must be a string.");
    }

    private static int Integer(Dictionary<string, object?> args, string key, int fallback, int min, int max)
    {
        if (!args.TryGetValue(key, out var value)) return fallback;
        if (!int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var number) || number < min || number > max)
            throw new ArgumentException($"{key} must be an integer between {min} and {max}.");
        return number;
    }
}

public interface IAgenticGuidanceVisual : IDisposable
{
    bool IsDisposed { get; }
    void Update(AgenticGuidanceOptions options);
}

/// <summary>只在所属 UI 线程访问；按服务端赋予的会话隔离引导，客户端不能伪造会话。</summary>
public sealed class AgenticGuidanceCollection : IDisposable
{
    private readonly Dictionary<string, IAgenticGuidanceVisual> _visuals = new(StringComparer.Ordinal);
    private static string Prefix(string? sessionId) => (sessionId ?? "local") + "\n";

    public void Show(AgenticCommand command, AgenticGuidanceOptions options, Func<IAgenticGuidanceVisual> create,
        Action<IAgenticGuidanceVisual>? configure = null)
    {
        foreach (var key in _visuals.Where(x => x.Value.IsDisposed).Select(x => x.Key).ToArray()) _visuals.Remove(key);
        var keyForVisual = Prefix(command.SessionId) + options.GuidanceId;
        if (!options.ShowOutline && !options.ShowNumber && !options.ShowBubble)
        {
            Remove(keyForVisual);
            return;
        }
        if (!_visuals.TryGetValue(keyForVisual, out var visual))
        {
            if (_visuals.Count >= 16) throw new InvalidOperationException("Too many concurrent guidance overlays on this control.");
            visual = create();
            _visuals.Add(keyForVisual, visual);
        }
        try { configure?.Invoke(visual); visual.Update(options); }
        catch { Remove(keyForVisual); throw; }
    }

    public void Clear(string? sessionId, string? guidanceId = null)
    {
        var prefix = Prefix(sessionId);
        foreach (var key in _visuals.Keys.Where(key => guidanceId is null
                     ? key.StartsWith(prefix, StringComparison.Ordinal) : key == prefix + guidanceId).ToArray())
            Remove(key);
    }

    private void Remove(string key)
    {
        if (!_visuals.TryGetValue(key, out var visual)) return;
        _visuals.Remove(key);
        visual.Dispose();
    }

    public void Dispose()
    {
        foreach (var key in _visuals.Keys.ToArray()) Remove(key);
    }
}

/// <summary>连接关闭后清理展示资源；不触发业务动作、不受当前模态窗口阻挡。</summary>
public interface IAgenticGuidanceControl
{
    Task ClearGuidanceAsync(string sessionId);
}
