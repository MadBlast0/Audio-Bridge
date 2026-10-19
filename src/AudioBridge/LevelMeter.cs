using System.Drawing.Drawing2D;

namespace AudioBridge;

internal sealed class LevelMeter : Control
{
    private float _level;
    public LevelMeter()
    {
        DoubleBuffered = true;
        Height = 12;
        MinimumSize = new Size(80, 10);
        BackColor = Color.White;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public float Level
    {
        get => _level;
        set
        {
            float next = Math.Clamp(value, 0f, 1f);
            if (Math.Abs(_level - next) < 0.015f) return;
            _level = next;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int segments = Math.Max(12, Width / 14);
        int gap = 3;
        float totalGap = gap * (segments - 1);
        float segmentWidth = Math.Max(2f, (Width - totalGap) / (float)segments);
        int filled = (int)Math.Round(_level * segments);
        using var background = new SolidBrush(Color.FromArgb(238, 238, 237));
        using var active = new SolidBrush(Color.FromArgb(52, 52, 52));
        using var high = new SolidBrush(Color.FromArgb(105, 105, 105));
        for (int i = 0; i < segments; i++)
        {
            float x = i * (segmentWidth + gap);
            var rect = new RectangleF(x, 1, segmentWidth, Math.Max(3, Height - 2));
            using var path = RoundedRect(rect, Math.Min(2.5f, segmentWidth / 2));
            e.Graphics.FillPath(i < filled ? (i >= segments - 3 ? high : active) : background, path);
        }
    }

    private static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float diameter = radius * 2;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
