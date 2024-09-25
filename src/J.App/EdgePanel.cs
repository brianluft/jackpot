namespace J.App;

public sealed class EdgePanel : Panel
{
    private readonly Bitmap _pageStartBitmap,
        _pagePreviousBitmap,
        _pageNextBitmap,
        _pageEndBitmap;
    private readonly bool _left;
    private readonly int _padding;
    private bool _jumpEnabled;

    public event EventHandler? ShortJump;
    public event EventHandler? LongJump;

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
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        if (JumpEnabled && e.Button == MouseButtons.Left)
        {
            if (e.Y >= Height - _pageEndBitmap.Height - _padding)
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

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Color.FromArgb(50, 50, 50));
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
