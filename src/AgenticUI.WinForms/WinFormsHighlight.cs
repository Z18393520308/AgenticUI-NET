using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AgenticUI;

namespace AgenticUI.WinForms;

internal interface IGuidanceOverlayWindow { }

internal sealed class WinFormsHighlight : IAgenticGuidanceVisual
{
    private static readonly Color Accent = Color.FromArgb(45, 125, 255);
    private readonly Control _control;
    private AgenticGuidanceOptions _options = new();
    private readonly System.Windows.Forms.Timer _timer = new();
    public bool IsDisposed { get; private set; }
    private Func<Rectangle>? _screenBoundsProvider;
    private readonly List<Control> _trackedContainers = new();
    private HighlightOverlayForm? _overlay;
    private Form? _ownerForm;

    public WinFormsHighlight(Control control, Func<Rectangle>? screenBoundsProvider = null)
    {
        _control = control;
        _screenBoundsProvider = screenBoundsProvider;
        _timer.Tick += OnExpired;
    }

    public void Update(AgenticGuidanceOptions options)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(WinFormsHighlight));
        _options = options;
        if (_overlay is null)
        {
            _ownerForm = _control.FindForm() ?? throw new InvalidOperationException("引导目标尚未挂载到窗口。");
            _overlay = new HighlightOverlayForm(_control.Font) { Owner = _ownerForm };
            _control.LocationChanged += OnLayoutChanged;
            _control.SizeChanged += OnLayoutChanged;
            _control.VisibleChanged += OnLayoutChanged;
            _control.Disposed += OnExpired;
            // 换父容器后原窗口/布局订阅已不再适用，关闭旧引导，等待控制端重新发现。
            _control.ParentChanged += OnExpired;
            if (_control is DataGridView grid)
            {
                grid.Scroll += OnGridScroll;
                grid.ColumnWidthChanged += OnGridColumnWidthChanged;
                grid.RowHeightChanged += OnGridRowHeightChanged;
                grid.Sorted += OnLayoutChanged;
                grid.RowsAdded += OnGridRowsAdded;
                grid.RowsRemoved += OnGridRowsRemoved;
                grid.DataBindingComplete += OnGridBindingComplete;
                grid.ColumnDisplayIndexChanged += OnGridColumnWidthChanged;
                grid.ColumnStateChanged += OnGridColumnStateChanged;
                grid.RowStateChanged += OnGridRowStateChanged;
            }
            _ownerForm.LocationChanged += OnLayoutChanged;
            _ownerForm.SizeChanged += OnLayoutChanged;
            _ownerForm.EnabledChanged += OnLayoutChanged;
            _ownerForm.Activated += OnLayoutChanged;
            TrackContainers();
        }
        _timer.Stop();
        if (options.DurationMs > 0)
        {
            _timer.Interval = options.DurationMs;
            _timer.Start();
        }
        UpdateOverlay();
    }

    private void OnExpired(object? sender, EventArgs args) => Dispose();

    public void Retarget(Func<Rectangle> screenBoundsProvider) => _screenBoundsProvider = screenBoundsProvider;

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        _timer.Stop();
        _timer.Dispose();
        _control.Disposed -= OnExpired;
        _control.ParentChanged -= OnExpired;
        _control.LocationChanged -= OnLayoutChanged;
        _control.SizeChanged -= OnLayoutChanged;
        _control.VisibleChanged -= OnLayoutChanged;
        if (_control is DataGridView grid)
        {
            grid.Scroll -= OnGridScroll;
            grid.ColumnWidthChanged -= OnGridColumnWidthChanged;
            grid.RowHeightChanged -= OnGridRowHeightChanged;
            grid.Sorted -= OnLayoutChanged;
            grid.RowsAdded -= OnGridRowsAdded;
            grid.RowsRemoved -= OnGridRowsRemoved;
            grid.DataBindingComplete -= OnGridBindingComplete;
            grid.ColumnDisplayIndexChanged -= OnGridColumnWidthChanged;
            grid.ColumnStateChanged -= OnGridColumnStateChanged;
            grid.RowStateChanged -= OnGridRowStateChanged;
        }
        if (_ownerForm is not null)
        {
            _ownerForm.LocationChanged -= OnLayoutChanged;
            _ownerForm.SizeChanged -= OnLayoutChanged;
            _ownerForm.EnabledChanged -= OnLayoutChanged;
            _ownerForm.Activated -= OnLayoutChanged;
        }

        foreach (var container in _trackedContainers)
        {
            container.Layout -= OnContainerLayout;
        }

        _trackedContainers.Clear();

        if (_overlay is not null)
        {
            _overlay.Close();
            _overlay.Dispose();
            _overlay = null;
        }

        _ownerForm = null;
    }

    private void TrackContainers()
    {
        for (var container = _control.Parent;
             container is not null;
             container = container.Parent)
        {
            _trackedContainers.Add(container);
            container.Layout += OnContainerLayout;
            if (ReferenceEquals(container, _ownerForm))
            {
                break;
            }
        }
    }

    private void OnLayoutChanged(object? sender, EventArgs args) => UpdateOverlay();
    private void OnContainerLayout(object? sender, LayoutEventArgs args) => UpdateOverlay();
    private void OnGridScroll(object? sender, ScrollEventArgs args) => UpdateOverlay();
    private void OnGridColumnWidthChanged(object? sender, DataGridViewColumnEventArgs args) => UpdateOverlay();
    private void OnGridRowHeightChanged(object? sender, DataGridViewRowEventArgs args) => UpdateOverlay();
    private void OnGridRowsAdded(object? sender, DataGridViewRowsAddedEventArgs args) => UpdateOverlay();
    private void OnGridRowsRemoved(object? sender, DataGridViewRowsRemovedEventArgs args) => UpdateOverlay();
    private void OnGridBindingComplete(object? sender, DataGridViewBindingCompleteEventArgs args) => UpdateOverlay();
    private void OnGridColumnStateChanged(object? sender, DataGridViewColumnStateChangedEventArgs args) => UpdateOverlay();
    private void OnGridRowStateChanged(object? sender, DataGridViewRowStateChangedEventArgs args) => UpdateOverlay();

    private void UpdateOverlay()
    {
        if (_overlay is null || _ownerForm is null || _ownerForm.WindowState == FormWindowState.Minimized ||
            !_control.Visible || !_control.IsHandleCreated || !WinFormsDisplayability.IsDisplayable(_control))
        {
            if (_overlay is not null)
            {
                _overlay.Visible = false;
            }

            return;
        }

        var screenBounds = _screenBoundsProvider?.Invoke() ??
                           _control.RectangleToScreen(_control.ClientRectangle);
        if (screenBounds.Width <= 0 || screenBounds.Height <= 0)
        {
            if (_screenBoundsProvider is not null) Dispose();
            else _overlay.Visible = false;
            return;
        }
        _overlay.UpdateTarget(screenBounds, _control.DeviceDpi, _options);
        if (!_overlay.Visible) _overlay.Show(_ownerForm);
    }

    private sealed class HighlightOverlayForm : Form, IGuidanceOverlayWindow
    {
        private const int WmNcHitTest = 0x0084;
        private const int HtTransparent = -1;
        private const int WsExNoActivate = 0x08000000;
        private const int WsExToolWindow = 0x00000080;
        private const int WsExTransparent = 0x00000020;
        private const int WsExLayered = 0x00080000;

        private AgenticGuidanceOptions _options = new();
        private Rectangle _outline;
        private Rectangle _badge;
        private Rectangle _bubble;
        private int _thickness;

        public HighlightOverlayForm(Font font)
        {
            Font = font;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;
            TabStop = false;
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= WsExNoActivate | WsExToolWindow | WsExTransparent | WsExLayered;
                return parameters;
            }
        }

        public void UpdateTarget(Rectangle screenTarget, int dpi, AgenticGuidanceOptions options)
        {
            _options = options;
            var scale = Math.Max(0.5F, dpi / 96F);
            _thickness = Math.Max(1, (int)Math.Round(3 * scale));
            var work = Screen.FromRectangle(screenTarget).WorkingArea;
            var hintSize = options.ShowBubble
                ? TextRenderer.MeasureText(options.Hint, Font,
                    new Size(Math.Max(20, Math.Min((int)(340 * scale), work.Width - 24)),
                        Math.Max(20, Math.Min((int)(240 * scale), work.Height - 24))),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis)
                : Size.Empty;
            var layout = AgenticGuidanceLayout.Calculate(
                new GuidanceRect(screenTarget.X, screenTarget.Y, screenTarget.Width, screenTarget.Height),
                new GuidanceRect(work.X, work.Y, work.Width, work.Height),
                hintSize.Width + 20 * scale, Math.Min(hintSize.Height, 240 * scale) + 16 * scale, scale, options);
            Bounds = ToRectangle(layout.Bounds);
            _outline = ToRectangle(layout.Outline);
            _badge = ToRectangle(layout.Badge);
            _bubble = ToRectangle(layout.Bubble);
            Invalidate();
        }

        private static Rectangle ToRectangle(GuidanceRect r) => r.IsEmpty ? Rectangle.Empty :
            new Rectangle((int)Math.Floor(r.X), (int)Math.Floor(r.Y),
                (int)Math.Ceiling(r.Width), (int)Math.Ceiling(r.Height));

        protected override void OnPaint(PaintEventArgs args)
        {
            base.OnPaint(args);
            args.Graphics.Clear(Color.Magenta);
            using var brush = new SolidBrush(Accent);
            if (!_outline.IsEmpty)
            {
                using var pen = new Pen(Accent, _thickness);
                args.Graphics.DrawRectangle(pen, _outline);
            }

            if (!_badge.IsEmpty)
            {
                args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                args.Graphics.FillEllipse(brush, _badge);
                TextRenderer.DrawText(
                    args.Graphics,
                    _options.InstructionNumber.ToString(),
                    Font,
                    _badge,
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            if (!_bubble.IsEmpty)
            {
                args.Graphics.FillRectangle(brush, _bubble);
                TextRenderer.DrawText(
                    args.Graphics,
                    _options.Hint,
                    Font,
                    Rectangle.Inflate(_bubble, -10, -8),
                    Color.White,
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WmNcHitTest)
            {
                message.Result = new IntPtr(HtTransparent);
                return;
            }

            base.WndProc(ref message);
        }
    }
}
