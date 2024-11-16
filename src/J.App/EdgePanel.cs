using System.ComponentModel;

namespace J.App;

public sealed class EdgePanel : Panel
{
    private readonly Bitmap _pageStartBitmap,
        _pagePreviousBitmap,
        _pageNextBitmap,
        _pageEndBitmap;
    private readonly bool _left;
    private readonly int _padding;
    private readonly int _longHeight;
    private bool _jumpEnabled;

    public event EventHandler? ShortJump;
    public event EventHandler? LongJump;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool JumpEnabled
    {
        get => _jumpEnabled;
        set
        {
            _jumpEnabled = value;
            Cursor = value ? Cursors.Hand : Cursors.No;
            Invalidate();
        }
    }

    public EdgePanel(bool left)
    {
        _left = left;
        Ui ui = new(this);
        _padding = ui.GetLength(5);
        Cursor = Cursors.Hand;
        _pageStartBitmap = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("PageStart.png", 16, 16));
        _pagePreviousBitmap = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("PagePrevious.png", 16, 16));
        _pageNextBitmap = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("PageNext.png", 16, 16));
        _pageEndBitmap = ui.InvertColorsInPlace(ui.GetScaledBitmapResource("PageEnd.png", 16, 16));
        _longHeight = _pageEndBitmap.Height + _padding * 2;
        DoubleBuffered = true;
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        if (JumpEnabled && e.Button == MouseButtons.Left)
        {
            if (e.Y >= Height - _longHeight)
            {
                LongJump?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ShortJump?.Invoke(this, EventArgs.Empty);
            }
        }
        else
        {
            base.OnMouseClick(e);
        }
    }

    private enum HoverState
    {
        Short,
        Long,
        None,
    }

    private HoverState _hover = HoverState.None;

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        _hover = e.Y < Height - _longHeight ? HoverState.Short : HoverState.Long;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = HoverState.None;
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;

        if (_jumpEnabled)
        {
            using SolidBrush normalBrush = new(Color.FromArgb(50, 50, 50));
            using SolidBrush hoverBrush = new(SystemColors.MenuHighlight);

            // Fill short jump area.
            g.FillRectangle(_hover == HoverState.Short ? hoverBrush : normalBrush, 0, 0, Width, Height - _longHeight);

            // Fill long jump area.
            g.FillRectangle(
                _hover == HoverState.Long ? hoverBrush : normalBrush,
                0,
                Height - _longHeight,
                Width,
                _longHeight
            );
        }
        else
        {
            g.Clear(Color.FromArgb(50, 50, 50));
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (!JumpEnabled)
            return;

        var g = e.Graphics;

        // Drawn centered vertically and horizontally.
        var middleBitmap = _left ? _pagePreviousBitmap : _pageNextBitmap;
        g.DrawImage(middleBitmap, (Width - middleBitmap.Width) / 2, (Height - middleBitmap.Height) / 2);

        // Drawn bottom vertically and centered horizontally.
        var bottomBitmap = _left ? _pageStartBitmap : _pageEndBitmap;
        g.DrawImage(bottomBitmap, (Width - bottomBitmap.Width) / 2, Height - bottomBitmap.Height - _padding);
    }
}
