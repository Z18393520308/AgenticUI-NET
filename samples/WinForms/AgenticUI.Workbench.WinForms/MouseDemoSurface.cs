using System.Drawing.Drawing2D;
using AgenticUI.WinForms;

namespace AgenticUI.Workbench.WinForms;

internal sealed class MouseDemoSurface : AgenticPanel
{
    private readonly List<Point> _trail = new();
    private Point _marker;
    private bool _dragging;
    private int _clicks;
    private int _markerSize = 24;
    private string _status = "等待鼠标动作";

    public MouseDemoSurface()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);
        BackColor = Color.FromArgb(243, 247, 255);
        BorderStyle = BorderStyle.FixedSingle;
        TabStop = true;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _marker = new Point(ClientSize.Width / 2, ClientSize.Height / 2);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        _marker = e.Location;
        if (e.Button == MouseButtons.Left)
        {
            _dragging = true;
            _clicks++;
            _trail.Clear();
            _trail.Add(e.Location);
            _status = $"按下并开始拖拽 ({e.X}, {e.Y})，累计点击 {_clicks} 次";
        }
        else if (e.Button == MouseButtons.Right)
        {
            _trail.Clear();
            _status = $"右键清除轨迹 ({e.X}, {e.Y})";
        }

        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _marker = e.Location;
        if (_dragging)
        {
            _trail.Add(e.Location);
            _status = $"拖拽中 ({e.X}, {e.Y})";
        }
        else
        {
            _status = $"移动到 ({e.X}, {e.Y})";
        }

        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _marker = e.Location;
        if (e.Button == MouseButtons.Left)
        {
            _dragging = false;
            _trail.Add(e.Location);
            _status = $"释放，拖拽结束于 ({e.X}, {e.Y})";
        }

        Invalidate();
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        _marker = e.Location;
        _status = $"双击 ({e.X}, {e.Y})";
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        _marker = e.Location;
        _markerSize = Math.Clamp(_markerSize + Math.Sign(e.Delta) * 4, 12, 64);
        _status = $"滚轮 {e.Delta}，圆点尺寸 {_markerSize}";
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var gridPen = new Pen(Color.FromArgb(224, 233, 248));
        for (var x = 20; x < ClientSize.Width; x += 20)
        {
            e.Graphics.DrawLine(gridPen, x, 0, x, ClientSize.Height);
        }
        for (var y = 20; y < ClientSize.Height; y += 20)
        {
            e.Graphics.DrawLine(gridPen, 0, y, ClientSize.Width, y);
        }

        if (_trail.Count > 1)
        {
            using var trailPen = new Pen(Color.FromArgb(110, 107, 155, 255), 3);
            e.Graphics.DrawLines(trailPen, _trail.ToArray());
        }

        var markerBounds = new Rectangle(
            _marker.X - _markerSize / 2,
            _marker.Y - _markerSize / 2,
            _markerSize,
            _markerSize);
        using var markerBrush = new SolidBrush(Color.FromArgb(40, 120, 255));
        using var markerPen = new Pen(Color.White, 3);
        e.Graphics.FillEllipse(markerBrush, markerBounds);
        e.Graphics.DrawEllipse(markerPen, markerBounds);

        TextRenderer.DrawText(
            e.Graphics,
            "蓝色圆点会跟随应用内鼠标消息；拖拽时显示轨迹。",
            Font,
            new Point(16, 14),
            Color.FromArgb(72, 101, 143));
        TextRenderer.DrawText(
            e.Graphics,
            "状态：" + _status,
            Font,
            new Point(16, ClientSize.Height - 30),
            Color.FromArgb(35, 65, 110));
    }
}
