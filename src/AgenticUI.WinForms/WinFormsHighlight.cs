using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AgenticUI.WinForms;

internal sealed class WinFormsHighlight : IDisposable
{
    private static readonly Color Accent = Color.FromArgb(45, 125, 255);
    private readonly Control _control;
    private readonly int _number;
    private readonly string? _hint;
    private readonly List<Control> _trackedContainers = new();
    private HighlightOverlayForm? _overlay;
    private Form? _ownerForm;

    public WinFormsHighlight(Control control, int number, string? hint)
    {
        _control = control;
        _number = number;
        _hint = hint;
    }

    public void Show()
    {
        if (_overlay is not null)
        {
            return;
        }

        _ownerForm = _control.FindForm();
        if (_ownerForm is null)
        {
            return;
        }

        // 独立顶层窗体，避免作为子控件被同窗体内其它控件遮挡。
        _overlay = new HighlightOverlayForm(_control.Font, _number, _hint)
        {
            Owner = _ownerForm,
            TopMost = true
        };
        _overlay.Show(_ownerForm);
        _control.LocationChanged += OnLayoutChanged;
        _control.SizeChanged += OnLayoutChanged;
        _control.VisibleChanged += OnLayoutChanged;
        _ownerForm.LocationChanged += OnLayoutChanged;
        _ownerForm.SizeChanged += OnLayoutChanged;
        TrackContainers();
        UpdateOverlay();
    }

    public void Dispose()
    {
        _control.LocationChanged -= OnLayoutChanged;
        _control.SizeChanged -= OnLayoutChanged;
        _control.VisibleChanged -= OnLayoutChanged;
        if (_ownerForm is not null)
        {
            _ownerForm.LocationChanged -= OnLayoutChanged;
            _ownerForm.SizeChanged -= OnLayoutChanged;
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

    private void UpdateOverlay()
    {
        if (_overlay is null || _ownerForm is null || !_control.Visible || !_control.IsHandleCreated)
        {
            if (_overlay is not null)
            {
                _overlay.Visible = false;
            }

            return;
        }

        var screenBounds = _control.RectangleToScreen(_control.ClientRectangle);
        _overlay.UpdateTarget(screenBounds, _control.DeviceDpi);
        _overlay.Visible = true;
        _overlay.TopMost = true;
        _overlay.BringToFront();
    }

    private sealed class HighlightOverlayForm : Form
    {
        private const int WmNcHitTest = 0x0084;
        private const int HtTransparent = -1;
        private const int WsExNoActivate = 0x08000000;
        private const int WsExToolWindow = 0x00000080;
        private const int WsExTransparent = 0x00000020;
        private const int WsExLayered = 0x00080000;

        private readonly int _number;
        private readonly string? _hint;
        private Rectangle _outline;
        private Rectangle _badge;
        private Rectangle _bubble;
        private int _thickness;

        public HighlightOverlayForm(Font font, int number, string? hint)
        {
            Font = font;
            _number = number;
            _hint = hint;
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

        public void UpdateTarget(Rectangle screenTarget, int dpi)
        {
            var scale = Math.Max(1F, dpi / 96F);
            _thickness = Math.Max(3, (int)Math.Round(3 * scale));
            var gap = Math.Max(4, (int)Math.Round(4 * scale));
            var padding = Math.Max(22, (int)Math.Round(22 * scale));
            var badgeSize = Math.Max(24, (int)Math.Round(24 * scale));
            var hintSize = string.IsNullOrWhiteSpace(_hint)
                ? Size.Empty
                : TextRenderer.MeasureText(_hint, Font);
            var bubbleHeight = hintSize.IsEmpty ? 0 : hintSize.Height + (int)Math.Round(10 * scale);
            var bubbleWidth = hintSize.IsEmpty ? 0 : hintSize.Width + (int)Math.Round(18 * scale);

            var width = Math.Max(screenTarget.Width + padding * 2, bubbleWidth + padding * 2);
            var height = screenTarget.Height + padding * 2 + (bubbleHeight == 0 ? 0 : bubbleHeight + gap);
            Bounds = new Rectangle(
                screenTarget.Left - padding,
                screenTarget.Top - padding,
                width,
                height);

            _outline = new Rectangle(
                padding - gap,
                padding - gap,
                screenTarget.Width + gap * 2,
                screenTarget.Height + gap * 2);
            _badge = _number > 0
                ? new Rectangle(
                    _outline.Left - badgeSize / 2,
                    _outline.Top - badgeSize / 2,
                    badgeSize,
                    badgeSize)
                : Rectangle.Empty;
            _bubble = hintSize.IsEmpty
                ? Rectangle.Empty
                : new Rectangle(_outline.Left, _outline.Bottom + gap, bubbleWidth, bubbleHeight);

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs args)
        {
            base.OnPaint(args);
            args.Graphics.Clear(Color.Magenta);
            using var brush = new SolidBrush(Accent);
            args.Graphics.FillRectangle(brush, _outline.Left, _outline.Top, _outline.Width, _thickness);
            args.Graphics.FillRectangle(
                brush,
                _outline.Left,
                _outline.Bottom - _thickness,
                _outline.Width,
                _thickness);
            args.Graphics.FillRectangle(brush, _outline.Left, _outline.Top, _thickness, _outline.Height);
            args.Graphics.FillRectangle(
                brush,
                _outline.Right - _thickness,
                _outline.Top,
                _thickness,
                _outline.Height);

            if (!_badge.IsEmpty)
            {
                args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                args.Graphics.FillEllipse(brush, _badge);
                TextRenderer.DrawText(
                    args.Graphics,
                    _number.ToString(),
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
                    _hint,
                    Font,
                    _bubble,
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
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
