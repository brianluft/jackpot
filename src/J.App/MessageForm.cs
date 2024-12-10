namespace J.App;

public sealed class MessageForm : Form
{
    private readonly Ui _ui;
    private readonly TableLayoutPanel _table;
    private readonly PictureBox _pictureBox;
    private readonly Label _label;
    private readonly FlowLayoutPanel _buttonFlow;

    private MessageForm(string message, string caption, MessageBoxIcon icon)
    {
        Ui ui = new(this);
        _ui = ui;

        var image = icon switch
        {
            MessageBoxIcon.Error or MessageBoxIcon.Warning => ui.InvertColorsInPlace(
                ui.GetScaledBitmapResource("Warning.png", 32, 32)
            ),
            MessageBoxIcon.Question => ui.InvertColorsInPlace(ui.GetScaledBitmapResource("Question.png", 32, 32)),
            MessageBoxIcon.Information => ui.InvertColorsInPlace(ui.GetScaledBitmapResource("Information.png", 32, 32)),
            _ => throw new Exception("Unsupported message box icon."),
        };

        Controls.Add(_table = ui.NewTable(2, 2));
        {
            _table.RowStyles[0].SizeType = SizeType.Percent;
            _table.RowStyles[0].Height = 100;

            _table.Controls.Add(_pictureBox = ui.NewPictureBox(image), 0, 0);
            {
                _table.SetRowSpan(_pictureBox, 2);
                _pictureBox.Margin += ui.RightSpacing;
            }

            _table.Controls.Add(_label = ui.NewLabel(message), 1, 0);
            {
                _label.MaximumSize = ui.GetSize(400, 1000);
            }

            _table.Controls.Add(_buttonFlow = ui.NewFlowRow(), 1, 1);
            {
                _buttonFlow.Dock = DockStyle.Right;
                _buttonFlow.Margin += ui.TopSpacingBig;
            }
        }

        Text = caption;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = ui.GetSize(350, 200);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        Padding = ui.DefaultPadding;
    }

    private MyButton AddButton(string text, DialogResult dialogResult)
    {
        var button = _ui.NewButton(text);
        button.DialogResult = dialogResult;
        button.Margin += _ui.ButtonSpacing;
        _buttonFlow.Controls.Add(button);
        return button;
    }

    public static DialogResult Show(
        IWin32Window? owner,
        string message,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        int defaultButtonIndex = 0
    )
    {
        if (buttons is not (MessageBoxButtons.OK or MessageBoxButtons.OKCancel))
            throw new ArgumentException("Unsupported buttons.", nameof(buttons));

        using MessageForm f = new(message, caption, icon);

        List<MyButton> buttonControls = [];

        if (buttons is MessageBoxButtons.OK or MessageBoxButtons.OKCancel)
            buttonControls.Add(f.AddButton("OK", DialogResult.OK));

        if (buttons is MessageBoxButtons.OKCancel)
            buttonControls.Add(f.AddButton("Cancel", DialogResult.Cancel));

        f.AcceptButton = buttonControls[defaultButtonIndex];
        f.CancelButton = buttonControls[^1];

        buttonControls[^1].Margin -= f._ui.ButtonSpacing;

        f.Shown += delegate
        {
            buttonControls[defaultButtonIndex].Focus();
        };
        f.DialogResult = buttonControls[^1].DialogResult;
        return owner is null ? f.ShowDialog() : f.ShowDialog(owner);
    }
}
