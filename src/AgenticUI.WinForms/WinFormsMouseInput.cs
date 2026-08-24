using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AgenticUI;

namespace AgenticUI.WinForms;

internal static class WinFormsMouseInput
{
    private const int WmMouseMove = 0x0200;
    private const int WmMouseWheel = 0x020A;

    public static void Execute(Control control, AgenticCommand command)
    {
        if (!control.Enabled)
        {
            throw new InvalidOperationException("控件当前已禁用，不能执行鼠标动作。");
        }

        var input = AgenticMouseInputParser.Parse(command);
        var start = ToClientPoint(control, input.Start);
        EnsureInteractable(control, start);

        switch (input.Action)
        {
            case AgenticActions.MouseMove:
                Send(control.Handle, WmMouseMove, IntPtr.Zero, PackPoint(start));
                break;
            case AgenticActions.MouseClick:
                Click(control.Handle, start, input.Button, doubleClick: false);
                break;
            case AgenticActions.MouseDoubleClick:
                Click(control.Handle, start, input.Button, doubleClick: true);
                break;
            case AgenticActions.MouseWheel:
                Wheel(control, start, input.WheelDelta);
                break;
            case AgenticActions.MouseDrag:
                Drag(control, start, ToClientPoint(control, input.End), input.Button, input.Steps);
                break;
            default:
                throw new ArgumentException($"Unsupported mouse action '{input.Action}'.");
        }
    }

    private static void Click(
        IntPtr handle,
        Point point,
        AgenticMouseButton button,
        bool doubleClick)
    {
        var messages = GetButtonMessages(button);
        var packedPoint = PackPoint(point);
        Send(handle, WmMouseMove, IntPtr.Zero, packedPoint);
        Send(handle, messages.Down, new IntPtr(messages.Mask), packedPoint);
        Send(handle, messages.Up, IntPtr.Zero, packedPoint);
        if (doubleClick)
        {
            Send(handle, messages.DoubleClick, new IntPtr(messages.Mask), packedPoint);
            Send(handle, messages.Up, IntPtr.Zero, packedPoint);
        }
    }

    private static void Wheel(Control control, Point point, int delta)
    {
        var screen = control.PointToScreen(point);
        var wheelData = new IntPtr((long)(delta & 0xffff) << 16);
        Send(control.Handle, WmMouseWheel, wheelData, PackPoint(screen));
    }

    private static void Drag(
        Control control,
        Point start,
        Point end,
        AgenticMouseButton button,
        int steps)
    {
        EnsureInteractable(control, end);
        var messages = GetButtonMessages(button);
        var lastPoint = start;
        var buttonIsDown = false;
        try
        {
            Send(control.Handle, WmMouseMove, IntPtr.Zero, PackPoint(start));
            Send(control.Handle, messages.Down, new IntPtr(messages.Mask), PackPoint(start));
            buttonIsDown = true;
            for (var step = 1; step <= steps; step++)
            {
                var progress = (double)step / steps;
                lastPoint = new Point(
                    (int)Math.Round(start.X + (end.X - start.X) * progress),
                    (int)Math.Round(start.Y + (end.Y - start.Y) * progress));
                EnsureInteractable(control, lastPoint);
                Send(control.Handle, WmMouseMove, new IntPtr(messages.Mask), PackPoint(lastPoint));
            }
        }
        finally
        {
            if (buttonIsDown)
            {
                Send(control.Handle, messages.Up, IntPtr.Zero, PackPoint(lastPoint));
            }
        }
    }

    private static Point ToClientPoint(Control control, AgenticMousePoint point) =>
        new(
            (int)Math.Round(point.XRatio * Math.Max(0, control.ClientSize.Width - 1)),
            (int)Math.Round(point.YRatio * Math.Max(0, control.ClientSize.Height - 1)));

    private static void EnsureInteractable(Control control, Point point)
    {
        if (!WinFormsDisplayability.IsPointInteractable(control, point))
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

    private static IntPtr PackPoint(Point point) =>
        new((point.X & 0xffff) | ((point.Y & 0xffff) << 16));

    private static void Send(IntPtr handle, int message, IntPtr wParam, IntPtr lParam) =>
        SendMessage(handle, message, wParam, lParam);

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
