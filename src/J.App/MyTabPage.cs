using J.Core;

namespace J.App;

public class MyTabPage : TabPage
{
    public MyTabPage()
    {
        BackColor = MyColors.TabPageBackground;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(MyColors.TabPageBackground);
    }
}
