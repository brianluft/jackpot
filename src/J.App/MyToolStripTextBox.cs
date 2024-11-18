namespace J.App;

public sealed class MyToolStripTextBox(Ui ui, Font cueFont)
    : ToolStripControlHost(new MyTextBox(ui) { CueFont = cueFont })
{
    public MyTextBox TextBox => (MyTextBox)Control;

    public void SetCueText(string text) => TextBox.SetCueText(text);
}
