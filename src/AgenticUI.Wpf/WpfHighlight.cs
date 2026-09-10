using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using AgenticUI;

namespace AgenticUI.Wpf;

internal interface IGuidanceOverlayWindow { }

internal sealed class WpfHighlight : IAgenticGuidanceVisual
{
    private FrameworkElement _element;
    private Func<bool>? _isTargetValid;
    private readonly DispatcherTimer _timer = new();
    private HighlightOverlayWindow? _overlay;
    private Window? _owner;
    private AgenticGuidanceOptions _options = new();
    private bool _updating;
    public bool IsDisposed { get; private set; }

    public WpfHighlight(FrameworkElement element, Func<bool>? isTargetValid = null)
    {
        _element = element;
        _isTargetValid = isTargetValid;
        _timer.Tick += OnExpired;
    }

    public void Update(AgenticGuidanceOptions options)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(WpfHighlight));
        _options = options;
        if (_overlay is null)
        {
            _owner = Window.GetWindow(_element) ?? throw new InvalidOperationException("引导目标尚未挂载到窗口。");
            _overlay = new HighlightOverlayWindow { Owner = _owner };
            _element.LayoutUpdated += OnLayout;
            _element.Unloaded += OnUnloaded;
            _owner.LocationChanged += OnLayout;
            _owner.SizeChanged += OnSize;
            _owner.StateChanged += OnLayout;
            _owner.Closed += OnExpired;
        }
        _timer.Stop();
        if (options.DurationMs > 0)
        {
            _timer.Interval = TimeSpan.FromMilliseconds(options.DurationMs);
            _timer.Start();
        }
        UpdateOverlay();
    }

    public void Retarget(FrameworkElement element, Func<bool> isTargetValid)
    {
        if (_overlay is not null && !ReferenceEquals(_element, element))
        {
            _element.LayoutUpdated -= OnLayout;
            _element.Unloaded -= OnUnloaded;
            element.LayoutUpdated += OnLayout;
            element.Unloaded += OnUnloaded;
        }
        _element = element;
        _isTargetValid = isTargetValid;
    }

    private void OnLayout(object? sender, EventArgs args) => UpdateOverlay();
    private void OnSize(object sender, SizeChangedEventArgs args) => UpdateOverlay();
    private void OnExpired(object? sender, EventArgs args) => Dispose();
    private void OnUnloaded(object sender, RoutedEventArgs args) => Dispose();

    private void UpdateOverlay()
    {
        if (_updating) return;
        _updating = true;
        try { UpdateOverlayCore(); }
        finally { _updating = false; }
    }

    private void UpdateOverlayCore()
    {
        if (_overlay is null || IsDisposed) return;
        if (_isTargetValid?.Invoke() == false) { Dispose(); return; }
        if (_owner is null || _owner.WindowState == WindowState.Minimized ||
            !_element.IsVisible || _element.ActualWidth <= 0 || _element.ActualHeight <= 0 ||
            PresentationSource.FromVisual(_element) is null || !WpfDisplayability.IsDisplayable(_element))
        {
            _overlay.Hide();
            return;
        }
        var bounds = new Rect(_element.PointToScreen(new Point()),
            _element.PointToScreen(new Point(_element.ActualWidth, _element.ActualHeight)));
        var source = PresentationSource.FromVisual(_element);
        var scale = source?.CompositionTarget?.TransformToDevice.M11 ?? 1d;
        _overlay.UpdateTarget(bounds, scale, _options);
        if (!_overlay.IsVisible) _overlay.Show();
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        _timer.Stop();
        _timer.Tick -= OnExpired;
        _element.LayoutUpdated -= OnLayout;
        _element.Unloaded -= OnUnloaded;
        if (_owner is not null)
        {
            _owner.LocationChanged -= OnLayout;
            _owner.SizeChanged -= OnSize;
            _owner.StateChanged -= OnLayout;
            _owner.Closed -= OnExpired;
        }
        _overlay?.Close();
        _overlay = null;
        _owner = null;
    }

    private sealed class HighlightOverlayWindow : Window, IGuidanceOverlayWindow
    {
        private AgenticGuidanceOptions _options = new();
        private Rect _outline, _badge, _bubble;
        private FormattedText? _hintText;
        private Rect _lastTarget;
        private double _lastScale;
        private NativeRect _lastWork;
        private AgenticGuidanceOptions? _lastOptions;
        private static readonly Brush Accent = CreateAccent();
        private static Brush CreateAccent()
        {
            var brush = new SolidColorBrush(Color.FromRgb(45, 125, 255));
            brush.Freeze();
            return brush;
        }
        public HighlightOverlayWindow()
        {
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
            var style = GetWindowLongPtr(hwnd, -20).ToInt64();
            _ = SetWindowLongPtr(hwnd, -20, new IntPtr(style | 0x08000000 | 0x00000080 | 0x00000020));
        }

        public void UpdateTarget(Rect target, double scale, AgenticGuidanceOptions options)
        {
            _options = options;
            scale = Math.Max(0.5, scale);
            var nativeRect = new NativeRect { Left = (int)target.Left, Top = (int)target.Top,
                Right = (int)Math.Ceiling(target.Right), Bottom = (int)Math.Ceiling(target.Bottom) };
            var monitor = MonitorFromRect(ref nativeRect, 2);
            var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
            if (!GetMonitorInfo(monitor, ref info)) throw new InvalidOperationException("无法读取引导目标所在屏幕。");
            // LayoutUpdated also fires for our overlay. An unchanged target must not
            // reposition or invalidate it again, otherwise layout never becomes idle.
            if (_lastTarget == target && _lastScale == scale &&
                _lastWork.Equals(info.Work) && ReferenceEquals(_lastOptions, options)) return;
            _lastTarget = target;
            _lastScale = scale;
            _lastWork = info.Work;
            _lastOptions = options;
            var work = new GuidanceRect(info.Work.Left, info.Work.Top,
                info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top);
            _hintText = options.ShowBubble ? CreateText(options.Hint!, 13) : null;
            if (_hintText is not null)
            {
                _hintText.MaxTextWidth = Math.Max(20, Math.Min(340, work.Width / scale - 24));
                _hintText.MaxTextHeight = Math.Max(20, Math.Min(240, work.Height / scale - 24));
                _hintText.Trimming = TextTrimming.CharacterEllipsis;
            }
            var layout = AgenticGuidanceLayout.Calculate(
                new GuidanceRect(target.X, target.Y, target.Width, target.Height), work,
                (_hintText?.Width ?? 0) * scale + 20 * scale,
                (_hintText?.Height ?? 0) * scale + 16 * scale, scale, options);
            // 屏幕定位使用物理像素，绘制尺寸转换成当前目标屏幕的 DIP。
            Width = Math.Max(1, layout.Bounds.Width / scale);
            Height = Math.Max(1, layout.Bounds.Height / scale);
            var handle = new WindowInteropHelper(this).EnsureHandle();
            SetWindowPos(handle, IntPtr.Zero, (int)Math.Floor(layout.Bounds.X), (int)Math.Floor(layout.Bounds.Y),
                (int)Math.Ceiling(layout.Bounds.Width), (int)Math.Ceiling(layout.Bounds.Height), 0x0010 | 0x0004);
            _outline = ToRect(layout.Outline, scale);
            _badge = ToRect(layout.Badge, scale);
            _bubble = ToRect(layout.Bubble, scale);
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext context)
        {
            base.OnRender(context);
            if (!_outline.IsEmpty) context.DrawRectangle(null, new Pen(Accent, 3), _outline);
            if (!_badge.IsEmpty)
            {
                context.DrawEllipse(Accent, null, new Point(_badge.X + _badge.Width / 2, _badge.Y + _badge.Height / 2),
                    _badge.Width / 2, _badge.Height / 2);
                var text = CreateText(_options.InstructionNumber.ToString(CultureInfo.InvariantCulture), 11);
                context.DrawText(text, new Point(_badge.X + (_badge.Width - text.Width) / 2,
                    _badge.Y + (_badge.Height - text.Height) / 2));
            }
            if (!_bubble.IsEmpty && _hintText is not null)
            {
                context.DrawRoundedRectangle(Accent, null, _bubble, 5, 5);
                context.DrawText(_hintText, new Point(_bubble.X + 10, _bubble.Y + 8));
            }
        }

        private static Rect ToRect(GuidanceRect r, double scale) => r.IsEmpty ? Rect.Empty
            : new Rect(r.X / scale, r.Y / scale, r.Width / scale, r.Height / scale);
        private static FormattedText CreateText(string text, double size) => new(text,
            CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, Brushes.White, 1);
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromRect(ref NativeRect rect, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
        private static IntPtr GetWindowLongPtr(IntPtr hwnd, int index) => IntPtr.Size == 8
            ? GetWindowLongPtr64(hwnd, index) : new IntPtr(GetWindowLong32(hwnd, index));
        private static IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value) => IntPtr.Size == 8
            ? SetWindowLongPtr64(hwnd, index, value) : new IntPtr(SetWindowLong32(hwnd, index, value.ToInt32()));
        [DllImport("user32.dll", EntryPoint = "GetWindowLong")] private static extern int GetWindowLong32(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")] private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")] private static extern int SetWindowLong32(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr value);
    }
}
