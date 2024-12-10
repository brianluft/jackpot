using System.ComponentModel;
using System.Drawing.Drawing2D;
using J.Core;

namespace J.App;

public sealed class MyTextBox : UserControl
{
    private readonly TextBox _textBox;
    private readonly Ui _ui;
    private bool _focused;

    public MyTextBox(Ui ui)
    {
        _ui = ui;
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw,
            true
        );

        _textBox = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = MyColors.TextBoxBackground,
            ForeColor = MyColors.TextBoxText,
            Location = new Point(_ui.GetLength(4), _ui.GetLength(4)),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        Controls.Add(_textBox);

        _textBox.GotFocus += (s, e) =>
        {
            _textBox.BackColor = MyColors.TextBoxFocusedBackground;
            _focused = true;
            Invalidate();
        };

        _textBox.LostFocus += (s, e) =>
        {
            _textBox.BackColor = MyColors.TextBoxBackground;
            _focused = false;
            Invalidate();
        };

        MinimumSize = new Size(_ui.GetLength(100), _ui.GetLength(32));
        Padding = _ui.GetPadding(8, 5);
        BackColor = Color.Transparent;
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateTextBoxBounds();
    }

    private void UpdateTextBoxBounds()
    {
        var padding = _ui.GetLength(8); // Padding between text and border

        // Calculate the height of a single line of text
        var singleLineHeight = TextRenderer.MeasureText("Tg", _textBox.Font).Height;

        _textBox.Size = new Size(Width - (padding * 2), Multiline ? Height - (padding * 2) : singleLineHeight);

        // Center the TextBox vertically if it's single line
        var yPos = Multiline ? padding : (Height - singleLineHeight) / 2;

        _textBox.Location = new Point(padding, yPos);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var scale = DeviceDpi / 96f;
        var hidpi = scale > 1;
        var cornerRadius = 4 * scale;

        RectangleF bounds = new(scale, scale, Width - (scale * 2) - 1, Height - (scale * 2) - 1);

        using var path = new GraphicsPath();
        path.AddRoundedRectangle(bounds, cornerRadius);

        // Fill with our background color
        using var bgBrush = new SolidBrush(
            Color.FromArgb(245, _focused ? MyColors.TextBoxFocusedBackground : MyColors.TextBoxBackground)
        );
        g.FillPath(bgBrush, path);

        using var borderPen = new Pen(MyColors.TextBoxBorder, scale);
        if (hidpi)
            g.TranslateTransform(0.5f, 0.5f);
        g.DrawPath(borderPen, path);
        g.ResetTransform();

        // Draw bottom border
        var bottomHeight = 2 * scale;
        RectangleF bottomRect =
            new(bounds.Left, bounds.Bottom - bottomHeight, bounds.Width, bottomHeight + (hidpi ? 1 : 0));
        var originalClip = g.Clip;
        var bottomPath = new GraphicsPath();
        bottomPath.AddRoundedRectangle(bounds, cornerRadius);

        if (_focused)
        {
            if (hidpi)
                g.TranslateTransform(0.5f, 1.5f);
            else
                g.TranslateTransform(0, 1);
            g.SetClip(bottomRect);
            using SolidBrush bottomPen = new(MyColors.TextBoxFocusedBottomBorder);
            g.FillPath(bottomPen, path);
        }
        else
        {
            if (hidpi)
                g.TranslateTransform(0.5f, 0.5f);
            bottomRect.Y++;
            g.SetClip(bottomRect);
            using Pen bottomPen = new(MyColors.TextBoxBottomBorder, 1 * scale);
            g.DrawPath(bottomPen, path);
        }

        g.ResetTransform();
        g.Clip = originalClip;

        // Draw cue text if needed
        if (string.IsNullOrEmpty(_textBox.Text) && !_focused && _textBox.Enabled)
        {
            var cueText = Tag as string;
            if (!string.IsNullOrEmpty(cueText))
            {
                var font = CueFont ?? _textBox.Font;
                var textSize = TextRenderer.MeasureText(g, cueText, font);
                var yPos = (Height - textSize.Height) / 2;

                TextRenderer.DrawText(
                    g,
                    cueText,
                    font,
                    new Point(_ui.GetLength(10), yPos),
                    MyColors.TextBoxCueText,
                    MyColors.TextBoxBackground,
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine
                );
            }
        }
    }

    // Forward common TextBox properties
    [Browsable(true)]
    public override string Text
    {
        get => _textBox.Text;
#pragma warning disable CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
        set => _textBox.Text = value;
#pragma warning restore CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
    }

    public char PasswordChar
    {
        get => _textBox.PasswordChar;
        set => _textBox.PasswordChar = value;
    }

    public bool ReadOnly
    {
        get => _textBox.ReadOnly;
        set => _textBox.ReadOnly = value;
    }

    public bool Multiline
    {
        get => _textBox.Multiline;
        set
        {
            _textBox.Multiline = value;
            if (value)
            {
                _textBox.WordWrap = WordWrap;
            }
            UpdateTextBoxBounds();
        }
    }

    public bool WordWrap
    {
        get => _textBox.WordWrap;
        set { _textBox.WordWrap = value && Multiline; }
    }

    public void SelectAll()
    {
        _textBox.SelectAll();
    }

    public void Select(int start, int length)
    {
        _textBox.Select(start, length);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Font? CueFont { get; set; }

    public void SetCueText(string text)
    {
        Tag = text;
        Invalidate();
    }
}

// Helper extension method for rounded rectangles
public static class GraphicsPathExtensions
{
    public static void AddRoundedRectangle(this GraphicsPath path, RectangleF bounds, float radius)
    {
        var diameter = radius * 2;
        RectangleF arc = new(bounds.Location, new SizeF(diameter, diameter));

        // Top left arc
        path.AddArc(arc, 180, 90);

        // Top right arc
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);

        // Bottom right arc
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);

        // Bottom left arc
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);

        path.CloseFigure();
    }
}
