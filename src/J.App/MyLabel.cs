using J.Core;

namespace J.App;

public class MyLabel : Label
{
    private bool _enabled = true;

    public new bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            ForeColor = _enabled ? MyColors.LabelForeground : MyColors.LabelForegroundDisabled;
        }
    }

    public MyLabel()
    {
        base.Enabled = true; // Always keep the underlying control enabled
        ForeColor = Color.White; // Initial state
    }
}
