using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgenticUI;

/// <summary>适配器在 UI 线程解析的选项；业务对象和兼容文本绝不进入协议载荷。</summary>
public sealed class AgenticItemEntry
{
    [JsonIgnore] public object? Source { get; }
    [JsonIgnore] public string? LegacyText { get; }
    public string? Text { get; }
    public string? ItemKey { get; }
    public bool? IsEnabled { get; }

    public AgenticItemEntry(object? source, string? text, string? itemKey = null,
        bool? isEnabled = true, string? legacyText = null)
    {
        Source = source;
        Text = text;
        ItemKey = itemKey;
        IsEnabled = isEnabled;
        LegacyText = legacyText;
    }
}

/// <summary>
/// 不含 UI 或 AI 依赖的选项协议。每个控件持有独立实例，由适配器在 UI 线程捕获当前视图。
/// 版本用于乐观校验，不是跨进程、跨重启的业务标识，也不替代业务事务。
/// </summary>
public sealed class AgenticItemCollection
{
    public const string Capability = "items.v1";
    private AgenticItemEntry[] _items = Array.Empty<AgenticItemEntry>();
    public string Version { get; private set; } = Guid.NewGuid().ToString("N");

    public void Update(IEnumerable<AgenticItemEntry> items)
    {
        var next = items.ToArray();
        if (next.Length != _items.Length || next.Where((item, i) => !Same(item, _items[i])).Any())
            Version = Guid.NewGuid().ToString("N");
        _items = next;
    }

    private static bool Same(AgenticItemEntry a, AgenticItemEntry b) =>
        (ReferenceEquals(a.Source, b.Source) ||
         (a.Source is string || a.Source?.GetType().IsValueType == true) && Equals(a.Source, b.Source)) &&
        // 容器生成/回收会改变已知可用状态，但不改变数据身份。可用性必须在选择时实时核验。
        a.Text == b.Text && a.ItemKey == b.ItemKey && a.LegacyText == b.LegacyText;

    public IReadOnlyDictionary<string, object?> ReadPage(AgenticCommand command, int selectedIndex)
    {
        ValidateVersion(command);
        var start = ReadInteger(command, "start", 0);
        var count = ReadInteger(command, "count", 50);
        if (start < 0 || count < 1 || count > 500)
            throw new ArgumentException("getItems requires start >= 0 and count between 1 and 500.");
        var page = _items.Skip(start).Take(count).Select((item, offset) =>
            (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
            {
                ["index"] = start + offset, ["text"] = item.Text, ["itemKey"] = item.ItemKey,
                ["isSelected"] = selectedIndex == start + offset, ["isEnabled"] = item.IsEnabled
            }).ToArray();
        return new Dictionary<string, object?>
        {
            ["items"] = page, ["start"] = start, ["count"] = page.Length,
            ["total"] = _items.Length, ["itemsVersion"] = Version
        };
    }

    public int ResolveIndex(AgenticCommand command)
    {
        var index = LocateIndex(command);
        if (_items[index].IsEnabled == false) throw new InvalidOperationException("目标选项已禁用。");
        if (_items[index].IsEnabled is null)
            throw new InvalidOperationException("无法确认选项可用状态；目标选项容器尚未就绪。");
        return index;
    }

    /// <summary>
    /// 仅定位候选项，供 UI 适配器按需生成容器；不代表获得选择权限。
    /// 完成容器准备后必须再次捕获状态，并使用 ResolveIndex 核验真实可用性。
    /// </summary>
    public int LocateIndex(AgenticCommand command)
    {
        ValidateVersion(command);
        var hasIndex = command.Arguments.ContainsKey("index");
        var hasKey = command.Arguments.ContainsKey("itemKey");
        var hasValue = command.Arguments.ContainsKey("value");
        if (hasKey && (hasIndex || hasValue))
            throw new ArgumentException("itemKey cannot be combined with index or value.");
        int index;
        // 兼容旧调用的 index 优先级，但格式错误的 index 不得回退到另一种选择方式。
        if (hasIndex)
        {
            index = ReadInteger(command, "index", -1);
            if (index < 0 || index >= _items.Length)
                throw new ArgumentOutOfRangeException("index", "Item index is outside the current list. Read getItems again.");
        }
        else if (hasKey)
        {
            var key = ReadString(command, "itemKey");
            index = FindUnique(item => item.ItemKey is not null &&
                string.Equals(item.ItemKey, key, StringComparison.Ordinal));
        }
        else if (hasValue)
        {
            var value = ReadString(command, "value");
            index = FindUnique(item => string.Equals(item.Text, value, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                index = FindUnique(item => item.Text is not null &&
                    string.Equals(item.Text.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase));
            // 历史客户端可能传过对象 ToString()。只在显示文字没有匹配时兼容；重名仍拒绝。
            if (index < 0)
                index = FindUnique(item => item.LegacyText is not null &&
                    string.Equals(item.LegacyText, value, StringComparison.OrdinalIgnoreCase));
        }
        else throw new ArgumentException("selectItem requires index, value, or itemKey.");

        if (index < 0) throw new ArgumentException("Item was not found. Use getItems to read the current options.");
        return index;
    }

    private int FindUnique(Func<AgenticItemEntry, bool> matches)
    {
        var found = -1;
        for (var i = 0; i < _items.Length; i++)
        {
            if (!matches(_items[i])) continue;
            if (found >= 0)
                throw new InvalidOperationException("Ambiguous item: multiple options match. Use a unique itemKey or a verified index with itemsVersion.");
            found = i;
        }
        return found;
    }

    private void ValidateVersion(AgenticCommand command)
    {
        if (command.Arguments.ContainsKey("itemsVersion") && ReadString(command, "itemsVersion") != Version)
            throw new InvalidOperationException("Items changed (stale itemsVersion). Read getItems again before selecting.");
    }

    private static int ReadInteger(AgenticCommand command, string name, int fallback)
    {
        if (!command.Arguments.TryGetValue(name, out var value)) return fallback;
        if (int.TryParse(value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)) return number;
        throw new ArgumentException($"{name} must be an integer.");
    }

    private static string ReadString(AgenticCommand command, string name)
    {
        var value = command.Arguments[name];
        if (value is string text) return text;
        if (value is JsonElement element && element.ValueKind == JsonValueKind.String) return element.GetString()!;
        // 保留 value 接受数字等简单值的历史用法，itemKey 和版本必须原样使用返回的字符串。
        if (name == "value" && ScalarKey(value) is string scalar) return scalar;
        throw new ArgumentException($"{name} must be a string.");
    }

    /// <summary>仅允许标量业务键，不能把 DTO、数据库行或任意对象序列化给远程端。</summary>
    public static string? ScalarKey(object? value) => value switch
    {
        null => null,
        string text => text,
        Guid guid => guid.ToString("D"),
        DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToString("O", CultureInfo.InvariantCulture),
        bool boolean => boolean ? "true" : "false",
        char character => character.ToString(),
        Enum enumeration => enumeration.ToString(),
        byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal =>
            Convert.ToString(value, CultureInfo.InvariantCulture),
        JsonElement json when json.ValueKind == JsonValueKind.String => json.GetString(),
        JsonElement json when json.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => json.ToString(),
        _ => null
    };
}
