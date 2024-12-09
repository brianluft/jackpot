using System.Drawing.Drawing2D;
using J.Core;

namespace J.App;

public class MyButton : Button
{
    private bool _isHovered = false;
    private bool _isPressed = false;

    public MyButton()
    {
        SetStyle(
            ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.Selectable,
            true
        );

        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        ForeColor = MyColors.ButtonText;
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _isHovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _isHovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isPressed = true;
            Invalidate();
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isPressed = false;
            Invalidate();
        }
        base.OnMouseUp(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
        {
            _isPressed = true;
            Invalidate();
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
        {
            _isPressed = false;
            Invalidate();
            PerformClick();
        }
        base.OnKeyUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var form = FindForm();
        if (form is null)
            return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Color.Transparent);

        // Calculate DPI scale factor
        float dpiScale = DeviceDpi / 96f;

        // Scale corner radius
        float cornerRadius = 4 * dpiScale;

        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        rect.Inflate(-3 * dpiScale, -3 * dpiScale);
        using var path = GetRoundedRectPath(rect, cornerRadius);

        // Determine background color based on state
        Color backgroundColor;
        var isAcceptButton = Enabled && ReferenceEquals(form.AcceptButton, this);

        if (isAcceptButton)
        {
            if (_isPressed)
                backgroundColor = MyColors.ButtonAcceptBackgroundPressed;
            else if (_isHovered)
                backgroundColor = MyColors.ButtonAcceptBackgroundHover;
            else
                backgroundColor = MyColors.ButtonAcceptBackgroundNormal;
        }
        else if (!Enabled)
            backgroundColor = MyColors.ButtonDisabledBack;
        else if (_isPressed)
            backgroundColor = MyColors.ButtonBackgroundPressed;
        else if (_isHovered)
            backgroundColor = MyColors.ButtonBackgroundHover;
        else
            backgroundColor = MyColors.ButtonBackgroundNormal;

        // Draw background
        using (var brush = new SolidBrush(backgroundColor))
        {
            g.FillPath(brush, path);
        }

        // Draw border
        if (isAcceptButton)
        {
            Color borderColor;
            if (_isPressed)
                borderColor = MyColors.ButtonAcceptBackgroundPressed;
            else if (_isHovered)
                borderColor = MyColors.ButtonAcceptBorderHover;
            else
                borderColor = MyColors.ButtonAcceptBorderNormal;

            using var pen = new Pen(borderColor, dpiScale);
            g.DrawPath(pen, path);
        }
        else if (Focused && Enabled && !_isPressed)
        {
            using var pen = new Pen(Color.Black, dpiScale);
            g.DrawPath(pen, path);
        }
        else if (Enabled)
        {
            using var pen = new Pen(MyColors.ButtonBorder, dpiScale);
            g.DrawPath(pen, path);
        }

        // Draw focus rectangle when the button has keyboard focus
        if (Focused && Enabled && !_isPressed && !isAcceptButton)
        {
            var focusRect = rect;

            focusRect.Inflate(1.5f * dpiScale, 1.5f * dpiScale);
            using var focusPen = new Pen(MyColors.ButtonFocusOutline, 2 * dpiScale);
            g.DrawPath(focusPen, GetRoundedRectPath(focusRect, cornerRadius + 1.5f * dpiScale));
        }

        // Draw text
        Color textColor;

        if (isAcceptButton)
        {
            if (_isPressed)
                textColor = MyColors.ButtonAcceptForegroundPressed;
            else
                textColor = MyColors.ButtonAcceptForegroundNormal;
        }
        else if (_isPressed)
            textColor = MyColors.ButtonForegroundPressed;
        else if (Enabled)
            textColor = MyColors.ButtonText;
        else
            textColor = MyColors.ButtonDisabledText;

        TextRenderer.DrawText(
            g,
            Text,
            Font,
            ClientRectangle,
            textColor,
            Color.Transparent,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
        );
    }

    private GraphicsPath GetRoundedRectPath(RectangleF rect, float radius)
    {
        float diameter = radius * 2;
        var size = new SizeF(diameter, diameter);
        var arc = new RectangleF(rect.Location, size);
        var path = new GraphicsPath();

        // Top left arc
        path.AddArc(arc, 180, 90);

        // Top right arc
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);

        // Bottom right arc
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);

        // Bottom left arc
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);

        path.CloseFigure();
        return path;
    }
}
