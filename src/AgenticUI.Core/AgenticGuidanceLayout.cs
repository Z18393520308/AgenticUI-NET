namespace AgenticUI;

/// <summary>不依赖桌面类型的像素几何，Web 适配器可遵循相同布局规则。</summary>
public readonly struct GuidanceRect
{
    public GuidanceRect(double x, double y, double width, double height)
    { X = x; Y = y; Width = Math.Max(0, width); Height = Math.Max(0, height); }
    public double X { get; }
    public double Y { get; }
    public double Width { get; }
    public double Height { get; }
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

public sealed class AgenticGuidanceLayout
{
    public GuidanceRect Bounds { get; private set; }
    public GuidanceRect Outline { get; private set; }
    public GuidanceRect Badge { get; private set; }
    public GuidanceRect Bubble { get; private set; }

    public static AgenticGuidanceLayout Calculate(GuidanceRect target, GuidanceRect workArea,
        double bubbleWidth, double bubbleHeight, double scale, AgenticGuidanceOptions options)
    {
        scale = Math.Max(0.5, scale);
        if (Intersect(target, workArea).IsEmpty) return new AgenticGuidanceLayout();
        var gap = 4 * scale;
        var badgeSize = 24 * scale;
        var outline = options.ShowOutline
            ? Intersect(new GuidanceRect(target.X - gap, target.Y - gap, target.Width + gap * 2, target.Height + gap * 2), workArea)
            : default;
        var badge = options.ShowNumber
            ? Fit(new GuidanceRect(target.X - badgeSize / 2, target.Y - badgeSize / 2, badgeSize, badgeSize), workArea)
            : default;
        var bubble = default(GuidanceRect);
        if (options.ShowBubble)
        {
            var width = Math.Min(bubbleWidth, workArea.Width);
            var height = Math.Min(bubbleHeight, workArea.Height);
            var bottomY = target.Bottom + gap * 2;
            var topY = target.Y - gap * 2 - height;
            var preferTop = options.Placement == "top" ||
                (options.Placement == "auto" && bottomY + height > workArea.Bottom);
            var y = preferTop ? topY : bottomY;
            if (y < workArea.Y && bottomY + height <= workArea.Bottom) y = bottomY;
            if (y + height > workArea.Bottom && topY >= workArea.Y) y = topY;
            bubble = Fit(new GuidanceRect(target.X, y, width, height), workArea);
        }
        var rects = new[] { outline, badge, bubble }.Where(r => !r.IsEmpty).ToArray();
        if (rects.Length == 0) return new AgenticGuidanceLayout();
        var left = Math.Max(workArea.X, rects.Min(r => r.X) - gap);
        var top = Math.Max(workArea.Y, rects.Min(r => r.Y) - gap);
        var right = Math.Min(workArea.Right, rects.Max(r => r.Right) + gap);
        var bottom = Math.Min(workArea.Bottom, rects.Max(r => r.Bottom) + gap);
        return new AgenticGuidanceLayout
        {
            Bounds = new GuidanceRect(left, top, right - left, bottom - top),
            Outline = Offset(outline, left, top), Badge = Offset(badge, left, top), Bubble = Offset(bubble, left, top)
        };
    }

    private static GuidanceRect Fit(GuidanceRect rect, GuidanceRect area)
    {
        var width = Math.Min(rect.Width, area.Width);
        var height = Math.Min(rect.Height, area.Height);
        return new GuidanceRect(Math.Max(area.X, Math.Min(rect.X, area.Right - width)),
            Math.Max(area.Y, Math.Min(rect.Y, area.Bottom - height)), width, height);
    }

    private static GuidanceRect Intersect(GuidanceRect rect, GuidanceRect area)
    {
        var left = Math.Max(rect.X, area.X); var top = Math.Max(rect.Y, area.Y);
        return new GuidanceRect(left, top, Math.Min(rect.Right, area.Right) - left, Math.Min(rect.Bottom, area.Bottom) - top);
    }

    private static GuidanceRect Offset(GuidanceRect rect, double x, double y) => rect.IsEmpty
        ? default : new GuidanceRect(rect.X - x, rect.Y - y, rect.Width, rect.Height);
}
