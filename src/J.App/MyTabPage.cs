namespace J.App;

public class MyTabPage : TabPage
{
    private readonly Color _backgroundColor = Color.FromArgb(45, 45, 45);

    public MyTabPage()
    {
        BackColor = _backgroundColor;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(_backgroundColor);
    }
}
