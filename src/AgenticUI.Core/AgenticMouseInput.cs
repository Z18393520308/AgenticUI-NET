using System.Globalization;
using System.Text.Json;

namespace AgenticUI;

public enum AgenticMouseButton
{
    Left,
    Right,
    Middle
}

public readonly struct AgenticMousePoint
{
    public AgenticMousePoint(double xRatio, double yRatio)
    {
        XRatio = xRatio;
        YRatio = yRatio;
    }

    public double XRatio { get; }
    public double YRatio { get; }
}

public sealed class AgenticMouseInput
{
    public string Action { get; init; } = "";
    public AgenticMousePoint Start { get; init; }
    public AgenticMousePoint End { get; init; }
    public AgenticMouseButton Button { get; init; }
    public int WheelDelta { get; init; }
    public int Steps { get; init; }
}

public static class AgenticMouseInputParser
{
    public static AgenticMouseInput Parse(AgenticCommand command)
    {
        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        var action = NormalizeAction(command.Action);
        var button = ReadButton(GetArgument(command, "button"));
        if (action == AgenticActions.MouseDrag)
        {
            return new AgenticMouseInput
            {
                Action = action,
                Start = new AgenticMousePoint(
                    ReadRequiredRatio(command, "startXRatio"),
                    ReadRequiredRatio(command, "startYRatio")),
                End = new AgenticMousePoint(
                    ReadRequiredRatio(command, "endXRatio"),
                    ReadRequiredRatio(command, "endYRatio")),
                Button = button,
                Steps = ReadInteger(GetArgument(command, "steps"), 10, 1, 100, "steps")
            };
        }

        var point = new AgenticMousePoint(
            ReadOptionalRatio(command, "xRatio", 0.5),
            ReadOptionalRatio(command, "yRatio", 0.5));
        return new AgenticMouseInput
        {
            Action = action,
            Start = point,
            End = point,
            Button = button,
            WheelDelta = action == AgenticActions.MouseWheel
                ? ReadInteger(GetArgument(command, "delta"), 120, -1200, 1200, "delta", rejectZero: true)
                : 0,
            Steps = 1
        };
    }

    private static string NormalizeAction(string action)
    {
        var supported = new[]
        {
            AgenticActions.MouseMove,
            AgenticActions.MouseClick,
            AgenticActions.MouseDoubleClick,
            AgenticActions.MouseWheel,
            AgenticActions.MouseDrag
        };
        var normalized = supported.FirstOrDefault(candidate =>
            string.Equals(candidate, action, StringComparison.OrdinalIgnoreCase));
        return normalized ?? throw new ArgumentException($"Unsupported mouse action '{action}'.");
    }

    private static AgenticMouseButton ReadButton(object? value)
    {
        var text = ReadText(value);
        if (string.IsNullOrWhiteSpace(text))
        {
            return AgenticMouseButton.Left;
        }

        if (Enum.TryParse<AgenticMouseButton>(text, ignoreCase: true, out var button))
        {
            return button;
        }

        throw new ArgumentException("button must be 'left', 'right', or 'middle'.");
    }

    private static double ReadRequiredRatio(AgenticCommand command, string key)
    {
        var value = GetArgument(command, key) ??
                    throw new ArgumentException($"mouseDrag requires '{key}'.");
        return ReadRatio(value, key);
    }

    private static double ReadOptionalRatio(AgenticCommand command, string key, double fallback)
    {
        var value = GetArgument(command, key);
        return value is null ? fallback : ReadRatio(value, key);
    }

    private static double ReadRatio(object value, string key)
    {
        var text = ReadText(value);
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var ratio) ||
            double.IsNaN(ratio) ||
            double.IsInfinity(ratio) ||
            ratio is < 0 or > 1)
        {
            throw new ArgumentException($"{key} must be a number between 0 and 1.");
        }

        return ratio;
    }

    private static int ReadInteger(
        object? value,
        int fallback,
        int minimum,
        int maximum,
        string key,
        bool rejectZero = false)
    {
        if (value is null)
        {
            return fallback;
        }

        var text = ReadText(value);
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ||
            number < minimum ||
            number > maximum ||
            (rejectZero && number == 0))
        {
            throw new ArgumentException(
                $"{key} must be an integer between {minimum} and {maximum}" +
                (rejectZero ? " and cannot be zero." : "."));
        }

        return number;
    }

    private static string? ReadText(object? value)
    {
        if (value is JsonElement json)
        {
            return json.ValueKind == JsonValueKind.String ? json.GetString() : json.ToString();
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static object? GetArgument(AgenticCommand command, string key) =>
        command.Arguments.TryGetValue(key, out var value) ? value : null;
}
