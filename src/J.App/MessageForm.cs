using System.Diagnostics;
using System.Media;
using J.Core;

namespace J.App;

public sealed class MessageForm : Form
{
    private readonly Ui _ui;
    private readonly TableLayoutPanel _table;
    private readonly PictureBox _pictureBox;
    private readonly MyLabel _label;
    private readonly FlowLayoutPanel _buttonFlow;
    private readonly System.Windows.Forms.Timer _timer;

    private MessageForm(string message, string caption, MessageBoxIcon icon, string? wikiUrl)
    {
        Ui ui = new(this);
        _ui = ui;

        var image = icon switch
        {
            MessageBoxIcon.Stop or MessageBoxIcon.Error or MessageBoxIcon.Warning => ui.InvertColorsInPlace(
                ui.GetScaledBitmapResource("Warning.png", 32, 32)
            ),
            MessageBoxIcon.Question => ui.InvertColorsInPlace(ui.GetScaledBitmapResource("Question.png", 32, 32)),
            MessageBoxIcon.Information => ui.InvertColorsInPlace(ui.GetScaledBitmapResource("Information.png", 32, 32)),
            _ => throw new Exception("Unsupported message box icon."),
        };

        Controls.Add(_table = ui.NewTable(2, 3));
        {
            _table.RowStyles[0].SizeType = SizeType.Percent;
            _table.RowStyles[0].Height = 100;

            _table.Controls.Add(_pictureBox = ui.NewPictureBox(image), 0, 0);
            {
                _table.SetRowSpan(_pictureBox, 3);
                _pictureBox.Margin += ui.RightSpacing;
            }

            _table.Controls.Add(_label = ui.NewLabel(message), 1, 0);
            {
                _label.MaximumSize = ui.GetSize(400, 1000);
            }

            if (wikiUrl is not null)
            {
                LinkLabel link;

                _table.Controls.Add(link = ui.NewLinkLabel("Read the Jackpot wiki for help on this error"), 1, 1);
                {
                    link.Margin += ui.TopSpacing;
                    link.LinkClicked += delegate
                    {
                        Process.Start(new ProcessStartInfo { FileName = wikiUrl!, UseShellExecute = true })!.Dispose();
                    };
                }
            }

            _table.Controls.Add(_buttonFlow = ui.NewFlowRow(), 1, 2);
            {
                _buttonFlow.Dock = DockStyle.Right;
                _buttonFlow.Margin += ui.TopSpacingBig;
            }
        }

        _timer = new() { Interval = 500, Enabled = false };
        _timer.Tick += Timer_Tick;

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

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _timer.Stop();

        foreach (Control control in _buttonFlow.Controls)
            control.Enabled = true;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_timer.Enabled)
            e.Cancel = true;

        base.OnFormClosing(e);
    }

    private MyButton AddButton(string text, DialogResult dialogResult)
    {
        var button = _ui.NewButton(text);
        button.DialogResult = dialogResult;
        button.Margin += _ui.ButtonSpacing;
        button.Enabled = false;
        _buttonFlow.Controls.Add(button);
        return button;
    }

    public static DialogResult Show(
        IWin32Window? owner,
        Exception exception,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        int defaultButtonIndex = 0
    )
    {
        var wikiUrl = exception is JException jex ? jex.WikiUrl : null;
        var message = exception is AggregateException aex ? aex.InnerExceptions.First().Message : exception.Message;
        return Show(owner, message, caption, buttons, icon, defaultButtonIndex, wikiUrl);
    }

    public static DialogResult Show(
        IWin32Window? owner,
        string message,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        int defaultButtonIndex = 0,
        string? wikiUrl = null
    )
    {
        if (buttons is not (MessageBoxButtons.OK or MessageBoxButtons.OKCancel))
            throw new ArgumentException("Unsupported buttons.", nameof(buttons));

        using MessageForm f = new(message, caption, icon, wikiUrl);

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

            if (icon == MessageBoxIcon.Information)
                SystemSounds.Exclamation.Play();
            else if (icon is MessageBoxIcon.Stop or MessageBoxIcon.Error or MessageBoxIcon.Warning)
                SystemSounds.Hand.Play();
            else if (icon == MessageBoxIcon.Question)
                SystemSounds.Question.Play();

            f._timer.Start();
        };
        f.DialogResult = buttonControls[^1].DialogResult;
        return owner is null ? f.ShowDialog() : f.ShowDialog(owner);
    }
}
