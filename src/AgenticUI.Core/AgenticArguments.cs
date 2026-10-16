using System.Globalization;
using System.Text.Json;

namespace AgenticUI;

/// <summary>
/// 按协议约定解析命令参数：数字与日期一律使用 JSON/不变区域性，不受目标机器系统区域性影响
/// （例如德语系统中 "1.5" 不能被当作 15）。
/// </summary>
public static class AgenticArguments
{
    public static double ReadDouble(object? value, string name)
    {
        var number = value switch
        {
            JsonElement { ValueKind: JsonValueKind.Number } json => json.GetDouble(),
            JsonElement { ValueKind: JsonValueKind.String } json => ParseDouble(json.GetString(), name),
            string text => ParseDouble(text, name),
            null => throw Missing(name),
            IConvertible convertible => convertible.ToDouble(CultureInfo.InvariantCulture),
            _ => throw Invalid(name, "number")
        };
        if (double.IsNaN(number) || double.IsInfinity(number)) throw Invalid(name, "finite number");
        return number;
    }

    public static decimal ReadDecimal(object? value, string name) => value switch
    {
        JsonElement { ValueKind: JsonValueKind.Number } json => json.TryGetDecimal(out var number) ? number : throw Invalid(name, "decimal"),
        JsonElement { ValueKind: JsonValueKind.String } json => ParseDecimal(json.GetString(), name),
        string text => ParseDecimal(text, name),
        null => throw Missing(name),
        IConvertible convertible => convertible.ToDecimal(CultureInfo.InvariantCulture),
        _ => throw Invalid(name, "decimal")
    };

    public static int ReadInt32(object? value, string name) => value switch
    {
        JsonElement { ValueKind: JsonValueKind.Number } json => json.TryGetInt32(out var number) ? number : throw Invalid(name, "integer"),
        JsonElement { ValueKind: JsonValueKind.String } json => ParseInt32(json.GetString(), name),
        string text => ParseInt32(text, name),
        null => throw Missing(name),
        int number => number,
        IConvertible convertible => ParseInt32(convertible.ToString(CultureInfo.InvariantCulture), name),
        _ => throw Invalid(name, "integer")
    };

    /// <summary>ISO-8601 或 yyyy-MM-dd；带时区标记的值转换为本地时间，与原生日期控件一致。</summary>
    public static DateTime ReadDateTime(object? value, string name)
    {
        if (value is DateTime date) return date;
        if (value is DateTimeOffset offset) return offset.LocalDateTime;
        var text = value switch
        {
            JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(),
            string raw => raw,
            null => throw Missing(name),
            _ => throw Invalid(name, "ISO-8601 date string")
        };
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed)) return parsed;
        throw Invalid(name, "ISO-8601 date string");
    }

    private static double ParseDouble(string? text, string name) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : throw Invalid(name, "number");

    private static decimal ParseDecimal(string? text, string name) =>
        decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : throw Invalid(name, "decimal");

    private static int ParseInt32(string? text, string name) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : throw Invalid(name, "integer");

    private static ArgumentException Missing(string name) => new($"Action requires a '{name}' argument.");
    private static ArgumentException Invalid(string name, string expected) => new($"'{name}' must be a {expected} (invariant culture).");
}
