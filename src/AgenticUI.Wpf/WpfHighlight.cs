using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace AgenticUI.Wpf;

internal sealed class WpfHighlight : IDisposable
{
    private static readonly Color AccentColor = Color.FromRgb(45, 125, 255);

    private readonly FrameworkElement _element;
    private readonly int _number;
    private readonly string? _hint;
    private HighlightOverlayWindow? _overlay;
    private Window? _ownerWindow;

    public WpfHighlight(FrameworkElement element, int number, string? hint)
    {
        _element = element;
        _number = number;
        _hint = hint;
    }

    public void Show()
    {
        if (_overlay is not null)
        {
            return;
        }

        _ownerWindow = Window.GetWindow(_element);
        if (_ownerWindow is null)
        {
            return;
        }

        // 独立顶层窗口，避免被同窗体内其它元素遮挡。
        _overlay = new HighlightOverlayWindow(_number, _hint)
        {
            Owner = _ownerWindow,
            Topmost = true
        };
        _overlay.Show();
        _element.LayoutUpdated += OnLayoutUpdated;
        _ownerWindow.LocationChanged += OnOwnerChanged;
        _ownerWindow.SizeChanged += OnOwnerSizeChanged;
        _ownerWindow.StateChanged += OnOwnerChanged;
        UpdateOverlay();
    }

    public void Dispose()
    {
        _element.LayoutUpdated -= OnLayoutUpdated;
        if (_ownerWindow is not null)
        {
            _ownerWindow.LocationChanged -= OnOwnerChanged;
            _ownerWindow.SizeChanged -= OnOwnerSizeChanged;
            _ownerWindow.StateChanged -= OnOwnerChanged;
        }

        if (_overlay is not null)
        {
            _overlay.Close();
            _overlay = null;
        }

        _ownerWindow = null;
    }

    private void OnLayoutUpdated(object? sender, EventArgs args) => UpdateOverlay();
    private void OnOwnerChanged(object? sender, EventArgs args) => UpdateOverlay();
    private void OnOwnerSizeChanged(object sender, SizeChangedEventArgs args) => UpdateOverlay();

    private void UpdateOverlay()
    {
        if (_overlay is null)
        {
            return;
        }

        if (_ownerWindow is null ||
            !_element.IsVisible ||
            _element.ActualWidth <= 0 ||
            _element.ActualHeight <= 0 ||
            PresentationSource.FromVisual(_element) is null)
        {
            _overlay.Visibility = Visibility.Hidden;
            return;
        }

        var topLeft = _element.PointToScreen(new Point(0, 0));
        var bottomRight = _element.PointToScreen(new Point(_element.ActualWidth, _element.ActualHeight));
        var screenBounds = new Rect(topLeft, bottomRight);
        _overlay.UpdateTarget(screenBounds, GetDpiScale());
        _overlay.Visibility = Visibility.Visible;
        _overlay.Topmost = true;
    }

    private double GetDpiScale()
    {
        var source = PresentationSource.FromVisual(_element);
        if (source?.CompositionTarget is null)
        {
            return 1;
        }

        return source.CompositionTarget.TransformToDevice.M11;
    }

    private sealed class HighlightOverlayWindow : Window
    {
        private const int GwlExStyle = -20;
        private const int WsExNoActivate = 0x08000000;
        private const int WsExToolWindow = 0x00000080;
        private const int WsExTransparent = 0x00000020;

        private readonly int _number;
        private readonly string? _hint;
        private Rect _outline;
        private Rect _badge;
        private Rect _bubble;
        private double _thickness = 3;

        public HighlightOverlayWindow(int number, string? hint)
        {
            _number = number;
            _hint = hint;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            Focusable = false;
            IsHitTestVisible = false;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt32();
            _ = SetWindowLongPtr(
                hwnd,
                GwlExStyle,
                new IntPtr(style | WsExNoActivate | WsExToolWindow | WsExTransparent));
        }

        public void UpdateTarget(Rect screenTarget, double dpiScale)
        {
            var scale = Math.Max(1d, dpiScale);
            _thickness = Math.Max(3d, 3d * scale);
            var gap = Math.Max(4d, 4d * scale);
            var padding = Math.Max(22d, 22d * scale);
            var badgeSize = Math.Max(24d, 24d * scale);

            var hintText = string.IsNullOrWhiteSpace(_hint) ? null : CreateText(_hint!, Brushes.White, 13 * scale);
            var bubbleHeight = hintText is null ? 0 : hintText.Height + 10 * scale;
            var bubbleWidth = hintText is null ? 0 : hintText.Width + 18 * scale;

            var width = Math.Max(screenTarget.Width + padding * 2, bubbleWidth + padding * 2);
            var height = screenTarget.Height + padding * 2 + (bubbleHeight == 0 ? 0 : bubbleHeight + gap);

            // PointToScreen 返回设备像素；窗口 Left/Top/Width/Height 使用 DIP。
            var toDip = 1d / scale;
            Left = (screenTarget.Left - padding) * toDip;
            Top = (screenTarget.Top - padding) * toDip;
            Width = width * toDip;
            Height = height * toDip;

            _outline = new Rect(
                (padding - gap) * toDip,
                (padding - gap) * toDip,
                (screenTarget.Width + gap * 2) * toDip,
                (screenTarget.Height + gap * 2) * toDip);
            _badge = _number > 0
                ? new Rect(
                    _outline.Left - badgeSize * toDip / 2,
                    _outline.Top - badgeSize * toDip / 2,
                    badgeSize * toDip,
                    badgeSize * toDip)
                : Rect.Empty;
            _bubble = hintText is null
                ? Rect.Empty
                : new Rect(_outline.Left, _outline.Bottom + gap * toDip, bubbleWidth * toDip, bubbleHeight * toDip);
            _thickness *= toDip;

            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            var accent = new SolidColorBrush(AccentColor);
            accent.Freeze();
            var pen = new Pen(accent, _thickness);
            pen.Freeze();
            drawingContext.DrawRectangle(null, pen, _outline);

            if (!_badge.IsEmpty)
            {
                drawingContext.DrawEllipse(
                    accent,
                    null,
                    new Point(_badge.X + _badge.Width / 2, _badge.Y + _badge.Height / 2),
                    _badge.Width / 2,
                    _badge.Height / 2);
                var number = CreateText(_number.ToString(CultureInfo.InvariantCulture), Brushes.White, Math.Max(10, _badge.Height * 0.45));
                drawingContext.DrawText(
                    number,
                    new Point(
                        _badge.X + (_badge.Width - number.Width) / 2,
                        _badge.Y + (_badge.Height - number.Height) / 2));
            }

            if (!_bubble.IsEmpty && !string.IsNullOrWhiteSpace(_hint))
            {
                drawingContext.DrawRoundedRectangle(accent, null, _bubble, 5, 5);
                var text = CreateText(_hint!, Brushes.White, Math.Max(11, _bubble.Height * 0.45));
                drawingContext.DrawText(
                    text,
                    new Point(
                        _bubble.X + (_bubble.Width - text.Width) / 2,
                        _bubble.Y + (_bubble.Height - text.Height) / 2));
            }
        }

        private static FormattedText CreateText(string text, Brush brush, double size)
        {
            return new FormattedText(
                text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                size,
                brush,
                1);
        }

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
            IntPtr.Size == 8
                ? GetWindowLongPtr64(hWnd, nIndex)
                : new IntPtr(GetWindowLong32(hWnd, nIndex));

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong) =>
            IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    }
}
