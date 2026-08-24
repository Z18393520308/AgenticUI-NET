using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using AgenticUI;

namespace AgenticUI.Wpf;

internal static class WpfMouseInput
{
    private const int WmMouseMove = 0x0200;
    private const int WmMouseWheel = 0x020A;

    public static void Execute(FrameworkElement element, AgenticCommand command)
    {
        if (!element.IsEnabled)
        {
            throw new InvalidOperationException("控件当前已禁用，不能执行鼠标动作。");
        }

        var window = Window.GetWindow(element) ??
                     throw new InvalidOperationException("控件不在活动窗口中。");
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("控件窗口句柄尚未创建。");
        }

        var input = AgenticMouseInputParser.Parse(command);
        var start = ToElementPoint(element, input.Start);
        EnsureInteractable(element, start);

        switch (input.Action)
        {
            case AgenticActions.MouseMove:
                SendClient(element, window, handle, WmMouseMove, IntPtr.Zero, start);
                break;
            case AgenticActions.MouseClick:
                Click(element, window, handle, start, input.Button, doubleClick: false);
                break;
            case AgenticActions.MouseDoubleClick:
                Click(element, window, handle, start, input.Button, doubleClick: true);
                break;
            case AgenticActions.MouseWheel:
                Wheel(element, handle, start, input.WheelDelta);
                break;
            case AgenticActions.MouseDrag:
                Drag(
                    element,
                    window,
                    handle,
                    start,
                    ToElementPoint(element, input.End),
                    input.Button,
                    input.Steps);
                break;
            default:
                throw new ArgumentException($"Unsupported mouse action '{input.Action}'.");
        }
    }

    private static void Click(
        FrameworkElement element,
        Window window,
        IntPtr handle,
        Point point,
        AgenticMouseButton button,
        bool doubleClick)
    {
        var messages = GetButtonMessages(button);
        SendClient(element, window, handle, WmMouseMove, IntPtr.Zero, point);
        SendClient(element, window, handle, messages.Down, new IntPtr(messages.Mask), point);
        SendClient(element, window, handle, messages.Up, IntPtr.Zero, point);
        if (doubleClick)
        {
            SendClient(element, window, handle, messages.DoubleClick, new IntPtr(messages.Mask), point);
            SendClient(element, window, handle, messages.Up, IntPtr.Zero, point);
        }
    }

    private static void Wheel(FrameworkElement element, IntPtr handle, Point point, int delta)
    {
        var screen = element.PointToScreen(point);
        var wheelData = new IntPtr((long)(delta & 0xffff) << 16);
        SendMessage(handle, WmMouseWheel, wheelData, PackPoint(screen));
    }

    private static void Drag(
        FrameworkElement element,
        Window window,
        IntPtr handle,
        Point start,
        Point end,
        AgenticMouseButton button,
        int steps)
    {
        EnsureInteractable(element, end);
        var messages = GetButtonMessages(button);
        var lastPoint = start;
        var buttonIsDown = false;
        try
        {
            SendClient(element, window, handle, WmMouseMove, IntPtr.Zero, start);
            SendClient(element, window, handle, messages.Down, new IntPtr(messages.Mask), start);
            buttonIsDown = true;
            for (var step = 1; step <= steps; step++)
            {
                var progress = (double)step / steps;
                lastPoint = new Point(
                    start.X + (end.X - start.X) * progress,
                    start.Y + (end.Y - start.Y) * progress);
                EnsureInteractable(element, lastPoint);
                SendClient(
                    element,
                    window,
                    handle,
                    WmMouseMove,
                    new IntPtr(messages.Mask),
                    lastPoint);
            }
        }
        finally
        {
            if (buttonIsDown)
            {
                SendClient(element, window, handle, messages.Up, IntPtr.Zero, lastPoint);
            }
        }
    }

    private static void SendClient(
        FrameworkElement element,
        Window window,
        IntPtr handle,
        int message,
        IntPtr wParam,
        Point elementPoint)
    {
        var windowPoint = element.TranslatePoint(elementPoint, window);
        var source = PresentationSource.FromVisual(window);
        var transform = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        var devicePoint = transform.Transform(windowPoint);
        SendMessage(handle, message, wParam, PackPoint(devicePoint));
    }

    private static Point ToElementPoint(FrameworkElement element, AgenticMousePoint point) =>
        new(
            point.XRatio * Math.Max(0, element.ActualWidth - 1),
            point.YRatio * Math.Max(0, element.ActualHeight - 1));

    private static void EnsureInteractable(FrameworkElement element, Point point)
    {
        if (!WpfDisplayability.IsPointInteractable(element, point))
        {
            throw new InvalidOperationException(
                "鼠标坐标不在当前应用的可交互控件区域内，动作已拒绝。");
        }
    }

    private static ButtonMessages GetButtonMessages(AgenticMouseButton button) => button switch
    {
        AgenticMouseButton.Left => new ButtonMessages(0x0201, 0x0202, 0x0203, 0x0001),
        AgenticMouseButton.Right => new ButtonMessages(0x0204, 0x0205, 0x0206, 0x0002),
        AgenticMouseButton.Middle => new ButtonMessages(0x0207, 0x0208, 0x0209, 0x0010),
        _ => throw new ArgumentOutOfRangeException(nameof(button))
    };

    private static IntPtr PackPoint(Point point)
    {
        var x = (int)Math.Round(point.X);
        var y = (int)Math.Round(point.Y);
        return new IntPtr((x & 0xffff) | ((y & 0xffff) << 16));
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private readonly struct ButtonMessages
    {
        public ButtonMessages(int down, int up, int doubleClick, int mask)
        {
            Down = down;
            Up = up;
            DoubleClick = doubleClick;
            Mask = mask;
        }

        public int Down { get; }
        public int Up { get; }
        public int DoubleClick { get; }
        public int Mask { get; }
    }
}
