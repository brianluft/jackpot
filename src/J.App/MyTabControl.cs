using System.Drawing.Drawing2D;

namespace J.App;

public sealed class MyTabControl : TabControl
{
    private readonly Color _backgroundColor = Color.FromArgb(32, 32, 32);
    private readonly Color _selectedTabColor = Color.FromArgb(45, 45, 45);
    private readonly Color _unselectedTabColor = Color.FromArgb(38, 38, 38);
    private readonly Color _borderColor = Color.FromArgb(60, 60, 60);
    private readonly Color _textColor = Color.FromArgb(240, 240, 240);
    private readonly Color _inactiveTextColor = Color.FromArgb(180, 180, 180);
    private readonly Font _font;

    public MyTabControl(Font font)
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer,
            true
        );
        _font = font;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(_backgroundColor);

        // Draw tab headers
        for (int i = 0; i < TabCount; i++)
        {
            DrawTab(g, i);
        }
    }

    private void DrawTab(Graphics g, int index)
    {
        var tabRect = GetTabRect(index);
        var isSelected = SelectedIndex == index;
        var dpiScale = DeviceDpi / 96d;
        var spacing = (int)(2 * dpiScale);

        // Adjust tab rectangle to create spacing between tabs
        tabRect.Inflate(-spacing, 0);
        tabRect.Height += spacing;

        // Fill and draw tab background
        using var path = CreateTabPath(tabRect, dpiScale);
        using SolidBrush fillBrush = new(isSelected ? _selectedTabColor : _unselectedTabColor);
        using SolidBrush textBrush = new(isSelected ? _textColor : _inactiveTextColor);
        using Pen borderPen = new(_borderColor);

        g.FillPath(fillBrush, path);
        g.DrawPath(borderPen, path);

        // Draw tab text
        var textFormat = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };

        // Adjust text rectangle with DPI-aware padding
        var textRect = tabRect;
        var padding = (int)(8 * dpiScale);
        textRect.Inflate(-padding / 2, 0);

        var tabText = TabPages[index].Text;
        g.DrawString(tabText, _font, textBrush, textRect, textFormat);
    }

    private GraphicsPath CreateTabPath(Rectangle rect, double dpiScale)
    {
        var path = new GraphicsPath();
        int radius = (int)(4 * dpiScale); // 4 pixels at 96 DPI

        // Top left corner
        path.AddArc(rect.Left, rect.Top, radius * 2, radius * 2, 180, 90);
        // Top right corner
        path.AddArc(rect.Right - radius * 2, rect.Top, radius * 2, radius * 2, 270, 90);
        // Bottom right corner
        path.AddLine(rect.Right, rect.Bottom, rect.Left, rect.Bottom);
        // Close path
        path.CloseFigure();

        return path;
    }
}
