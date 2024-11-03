using System.Runtime.ExceptionServices;

namespace J.App;

public sealed class SimpleProgressForm : Form
{
    private readonly CancellationTokenSource _cts = new();
    private readonly TableLayoutPanel _table;
    private readonly Label _label;
    private readonly ProgressBar _progressBar;
    private readonly FlowLayoutPanel _buttonFlow;
    private readonly Button _cancelButton;

    public delegate void WorkDelegate(
        Action<double> updateProgress,
        Action<string> updateMessage,
        CancellationToken cancel
    );

    public ExceptionDispatchInfo? Exception { get; private set; }

    public static void Do(IWin32Window owner, string text, Func<CancellationToken, Task> action)
    {
        Do(owner, text, cancel => action(cancel).GetAwaiter().GetResult());
    }

    public static void Do(IWin32Window owner, string text, Action<CancellationToken> action)
    {
        using SimpleProgressForm f =
            new(
                (updateProgress, updateMessage, cancel) =>
                {
                    updateMessage(text);
                    action(cancel);
                },
                false
            );
        var result = f.ShowDialog(owner);
        if (result == DialogResult.Abort)
            f.Exception!.Throw();
        else if (result == DialogResult.Cancel)
            throw new OperationCanceledException();
    }

    public SimpleProgressForm(WorkDelegate action, bool progressBar = true)
    {
        Ui ui = new(this);

        Controls.Add(_table = ui.NewTable(2, 3));
        {
            _table.Padding = ui.DefaultPadding;
            _table.RowStyles[0].SizeType = SizeType.Percent;
            _table.RowStyles[0].Height = 100;
            _table.ColumnStyles[1].SizeType = SizeType.Percent;
            _table.ColumnStyles[1].Width = 100;

            _table.Controls.Add(ui.NewPictureBox(ui.GetScaledBitmapResource("App.png", 32, 32)), 0, 0);

            _table.Controls.Add(_label = ui.NewLabel("Starting."), 1, 0);

            _table.Controls.Add(_progressBar = ui.NewProgressBar(300), 0, 1);
            {
                _table.SetColumnSpan(_progressBar, 2);
                _progressBar.Margin = ui.TopSpacingBig + ui.BottomSpacingBig;
                if (!progressBar)
                {
                    _progressBar.Style = ProgressBarStyle.Marquee;
                    _progressBar.MarqueeAnimationSpeed = 10;
                }
            }

            _table.Controls.Add(_buttonFlow = ui.NewFlowRow(), 1, 2);
            {
                _buttonFlow.Dock = DockStyle.Right;

                _buttonFlow.Controls.Add(_cancelButton = ui.NewButton("Cancel"));
                _cancelButton.Click += CancelButton_Click;
            }
        }

        Text = "Progress";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = ui.GetSize(300, 150);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowOnly;
        FormBorderStyle = FormBorderStyle.Fixed3D;
        MinimizeBox = false;
        MaximizeBox = false;
        CancelButton = _cancelButton;
        ShowIcon = false;
        ShowInTaskbar = false;

        Shown += async delegate
        {
            try
            {
                await Task.Run(() => action(UpdateProgress, UpdateMessage, _cts.Token)).ConfigureAwait(true);
                DialogResult = DialogResult.OK;
            }
            catch (OperationCanceledException)
            {
                DialogResult = DialogResult.Cancel;
            }
            catch (Exception ex)
            {
                Exception = ExceptionDispatchInfo.Capture(ex);
                DialogResult = DialogResult.Abort;
            }
            finally
            {
                Close();
            }
        };
    }

    private void UpdateMessage(string message)
    {
        if (InvokeRequired)
            BeginInvoke(() => UpdateMessage(message));
        else
            _label.Text = message;
    }

    private void UpdateProgress(double progress)
    {
        if (InvokeRequired)
            BeginInvoke(() => UpdateProgress(progress));
        else
            _progressBar.Value = (int)(progress * _progressBar.Maximum);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        _cts.Cancel();
    }

    private void CancelButton_Click(object? sender, EventArgs e)
    {
        _cts.Cancel();
        _cancelButton.Enabled = false;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _cts.Dispose();
        }
    }
}
