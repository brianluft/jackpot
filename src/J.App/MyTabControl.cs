using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace J.App;

public sealed partial class MyTabControl : TabControl
{
    private readonly Font _font,
        _boldFont;

    public MyTabControl(Font font, Font boldFont)
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer,
            true
        );
        _font = font;
        _boldFont = boldFont;
        Multiline = true;
    }

    public void ManipulateTabs(Action action)
    {
        var form = FindForm()!;
        form.SuspendLayout();
        NativeMethods.SendMessageW(form.Handle, NativeMethods.WM_SETREDRAW, false, 0);
        try
        {
            action();
        }
        finally
        {
            NativeMethods.SendMessageW(form.Handle, NativeMethods.WM_SETREDRAW, true, 0);
            form.ResumeLayout();
            form.Refresh();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(MyColors.TabBackground);

        // Draw tab headers, in reverse order so we are painting back to front.

        var indices = from i in Enumerable.Range(0, TabCount) let rect = GetTabRect(i) orderby rect.Y, rect.X select i;

        foreach (var i in indices)
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
        using SolidBrush fillBrush = new(isSelected ? MyColors.TabSelected : MyColors.TabUnselected);
        using SolidBrush textBrush = new(isSelected ? MyColors.TabText : MyColors.TabInactiveText);
        using Pen borderPen = new(MyColors.TabBorder);

        g.FillPath(fillBrush, path);
        g.DrawPath(borderPen, path);

        // Draw tab text
        var textFormat = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
        };

        // Adjust text rectangle with DPI-aware padding
        var textRect = tabRect;
        var padding = (int)(8 * dpiScale);
        textRect.Inflate(-padding / 2, 0);

        var tabText = TabPages[index].Text;
        g.DrawString(tabText, isSelected ? _boldFont : _font, textBrush, textRect, textFormat);
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

    private static partial class NativeMethods
    {
        [LibraryImport("user32.dll")]
        public static partial int SendMessageW(
            IntPtr hWnd,
            int wMsg,
            [MarshalAs(UnmanagedType.Bool)] bool wParam,
            int lParam
        );

        public const int WM_SETREDRAW = 11;
    }
}
