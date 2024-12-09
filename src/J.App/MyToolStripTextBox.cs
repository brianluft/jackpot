using System.ComponentModel;
using J.Core;

namespace J.App;

public sealed class MyToolStripTextBox(Ui ui, Font cueFont)
    : ToolStripControlHost(new InnerTextBox(ui) { CueFont = cueFont })
{
    public InnerTextBox TextBox => (InnerTextBox)Control;

    public void SetCueText(string text) => TextBox.SetCueText(text);

    public sealed class InnerTextBox(Ui ui) : TextBox
    {
        private const int WM_PAINT = 0x000F;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Font? CueFont { get; set; }

        public void SetCueText(string text)
        {
            Tag = text;
            Invalidate();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == WM_PAINT)
            {
                if (!string.IsNullOrEmpty(Text) || Focused || !Enabled)
                    return;

                var cueText = Tag as string;
                if (!string.IsNullOrEmpty(cueText))
                {
                    using var g = Graphics.FromHwnd(Handle);

                    // Calculate vertical center
                    var font = CueFont ?? Font;
                    var textSize = TextRenderer.MeasureText(g, cueText, font);
                    var yPos = (ClientSize.Height - textSize.Height) / 2 - ui.GetLength(1);

                    TextRenderer.DrawText(
                        g,
                        cueText,
                        font,
                        new Point(ui.GetLength(2), yPos),
                        MyColors.TextBoxCueText,
                        BackColor,
                        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine
                    );
                }
            }
        }
    }
}
