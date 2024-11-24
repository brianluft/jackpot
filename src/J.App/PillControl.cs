using System.ComponentModel;

namespace J.App;

public sealed class PillControl : Control
{
    private const int HORIZONTAL_PADDING = 8;
    private const int CLOSE_BUTTON_SIZE = 16;
    private const int CLOSE_BUTTON_PADDING = 4;
    private const string CLOSE_SYMBOL = "🞭";

    private Color _tagColor = Color.FromArgb(64, 64, 64);
    private Color _pressColor = Color.FromArgb(96, 96, 96);
    private Color _focusedBorderColor = Color.FromArgb(0, 120, 215);
    private Color _textColor = Color.FromArgb(240, 240, 240);
    private bool _isHovered = false;
    private bool _isPressed = false;
    private Rectangle _closeButtonRect;

    public event EventHandler? TagRemoved;

    [Category("Appearance"), DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public Color TagColor
    {
        get => _tagColor;
        set
        {
            _tagColor = value;
            Invalidate();
        }
    }

    [Category("Appearance"), DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public Color PressColor
    {
        get => _pressColor;
        set
        {
            _pressColor = value;
            Invalidate();
        }
    }

    [Category("Appearance"), DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public Color FocusedBorderColor
    {
        get => _focusedBorderColor;
        set
        {
            _focusedBorderColor = value;
            Invalidate();
        }
    }

    [Category("Appearance"), DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public Color TextColor
    {
        get => _textColor;
        set
        {
            _textColor = value;
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override string Text
    {
        get => base.Text;
#pragma warning disable CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
        set
#pragma warning restore CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
        {
            base.Text = value;
            UpdateSize();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Font Font
    {
        get => base.Font;
#pragma warning disable CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
        set
#pragma warning restore CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
        {
            base.Font = value;
            UpdateSize();
        }
    }

    public PillControl()
    {
        SetStyle(
            ControlStyles.SupportsTransparentBackColor
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw,
            true
        );

        TabStop = true;
        Cursor = Cursors.Hand;

        UpdateSize();
    }

    private void UpdateSize()
    {
        using var g = CreateGraphics();

        // Measure text
        var textSize = g.MeasureString(Text, Font);

        // Calculate total width needed:
        // Left half-circle + text + padding + close button + right half-circle
        int height = (int)Math.Ceiling(textSize.Height) + 6; // Add some vertical padding
        int width =
            (int)Math.Ceiling(textSize.Width)
            + (height * 2)
            + // Left and right half-circles (diameter equals height)
            (HORIZONTAL_PADDING * 2)
            + // Padding between text and circles
            CLOSE_BUTTON_SIZE
            + // Space for close button
            (CLOSE_BUTTON_PADDING * 2); // Padding around close button

        Size = new Size(width, height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        int diameter = Height;
        Rectangle leftCircle = new(0, 0, diameter, diameter);
        Rectangle rightCircle = new(Width - diameter, 0, diameter, diameter);
        Rectangle centerRect = new(diameter / 2, 0, Width - diameter, Height);

        // Determine background color based on state
        Color currentBackColor = _isPressed ? _pressColor : _tagColor;
        using (var brush = new SolidBrush(currentBackColor))
        {
            // Draw left circle
            g.FillEllipse(brush, leftCircle);
            // Draw center rectangle
            g.FillRectangle(brush, centerRect);
            // Draw right circle
            g.FillEllipse(brush, rightCircle);
        }

        // Draw focus border if control has focus
        if (Focused)
        {
            using var pen = new Pen(_focusedBorderColor, 1);
            g.DrawEllipse(pen, leftCircle);
            g.DrawLines(
                pen,
                [
                    new(diameter / 2, 0),
                    new(Width - diameter / 2, 0),
                    new(Width - diameter / 2, Height - 1),
                    new(diameter / 2, Height - 1),
                ]
            );
            g.DrawEllipse(pen, rightCircle);
        }

        // Calculate text bounds (leaving space for close button)
        Rectangle textRect =
            new(
                diameter / 2 + HORIZONTAL_PADDING,
                0,
                Width - diameter - (HORIZONTAL_PADDING * 2) - CLOSE_BUTTON_SIZE - (CLOSE_BUTTON_PADDING * 2),
                Height
            );

        // Draw text
        TextRenderer.DrawText(
            g,
            Text,
            Font,
            textRect,
            _textColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
        );

        // Draw close button if hovered
        if (_isHovered)
        {
            _closeButtonRect = new Rectangle(
                Width - diameter - CLOSE_BUTTON_PADDING - CLOSE_BUTTON_SIZE,
                (Height - CLOSE_BUTTON_SIZE) / 2,
                CLOSE_BUTTON_SIZE,
                CLOSE_BUTTON_SIZE
            );

            using var brush = new SolidBrush(_textColor);
            g.DrawString(CLOSE_SYMBOL, Font, brush, _closeButtonRect);
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        _isPressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            _isPressed = true;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left && _isPressed)
        {
            _isPressed = false;
            if (_isHovered)
            {
                if (_closeButtonRect.Contains(e.Location))
                {
                    TagRemoved?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    OnClick(EventArgs.Empty);
                }
            }
            Invalidate();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Enter)
        {
            OnClick(EventArgs.Empty);
        }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }
}
