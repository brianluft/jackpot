using System.ComponentModel;
using System.Runtime.ExceptionServices;

namespace J.App;

public enum Outcome
{
    Success,
    Failure,
    Cancellation,
}

public sealed class ProgressForm : Form
{
    private readonly CancellationTokenSource _cts = new();
    private readonly TableLayoutPanel _table;
    private readonly MyLabel _label;
    private readonly ProgressBar _progressBar;
    private readonly FlowLayoutPanel _buttonFlow;
    private readonly Button _cancelButton;
    private bool _allowClose = false;

    public delegate void WorkDelegate(
        Action<double> updateProgress,
        Action<string> updateMessage,
        CancellationToken cancel
    );

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ExceptionDispatchInfo? Exception { get; private set; }

    public static Outcome Do(IWin32Window? owner, string text, Func<Action<double>, CancellationToken, Task> action)
    {
        return Do(owner, text, (updateProgress, _, cancel) => action(updateProgress, cancel));
    }

    public static Outcome Do(
        IWin32Window? owner,
        string text,
        Func<Action<double>, Action<string>, CancellationToken, Task> action
    )
    {
        using ProgressForm f =
            new(
                (updateProgress, updateMessage, cancel) =>
                {
                    updateMessage(text);
                    action(updateProgress, updateMessage, cancel).GetAwaiter().GetResult();
                }
            );

        var result = owner is null ? f.ShowDialog() : f.ShowDialog(owner);

        switch (result)
        {
            case DialogResult.Cancel:
                return Outcome.Cancellation;

            case DialogResult.Abort:
                MessageForm.Show(
                    owner,
                    f.Exception!.SourceException.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return Outcome.Failure;

            default:
                return Outcome.Success;
        }
    }

    public static void DoModeless(
        IWin32Window? owner,
        string text,
        Func<Action<double>, Action<string>, CancellationToken, Task> action,
        Action<Outcome> continuation
    )
    {
        ProgressForm f =
            new(
                (updateProgress, updateMessage, cancel) =>
                {
                    updateMessage(text);
                    action(updateProgress, updateMessage, cancel).GetAwaiter().GetResult();
                }
            )
            {
                ShowInTaskbar = true,
            };

        f.FormClosed += delegate
        {
            switch (f.DialogResult)
            {
                case DialogResult.Cancel:
                    continuation(Outcome.Cancellation);
                    break;

                case DialogResult.Abort:
                    MessageForm.Show(
                        owner,
                        f.Exception!.SourceException.Message,
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    continuation(Outcome.Failure);
                    break;

                default:
                    continuation(Outcome.Success);
                    break;
            }
        };

        if (owner is null)
            f.Show();
        else
            f.Show(owner);
    }

    private ProgressForm(WorkDelegate action)
    {
        Ui ui = new(this);

        Controls.Add(_table = ui.NewTable(1, 3));
        {
            _table.Padding = ui.DefaultPadding;
            _table.RowStyles[0].SizeType = SizeType.Percent;
            _table.RowStyles[0].Height = 100;

            _table.Controls.Add(_label = ui.NewLabel("Starting."), 0, 0);

            _table.Controls.Add(_progressBar = ui.NewProgressBar(450), 0, 1);
            {
                _table.SetColumnSpan(_progressBar, 2);
                _progressBar.Margin = ui.TopSpacing + ui.BottomSpacingBig;
            }

            _table.Controls.Add(_buttonFlow = ui.NewFlowRow(), 0, 2);
            {
                _buttonFlow.Dock = DockStyle.Right;

                _buttonFlow.Controls.Add(_cancelButton = ui.NewButton("Cancel"));
                {
                    _cancelButton.Click += CancelButton_Click;
                    _cancelButton.TabStop = false;
                }
            }
        }

        Text = "Progress";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new(0, 0);
        MinimumSize = ui.GetSize(0, 250);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowOnly;
        FormBorderStyle = FormBorderStyle.Fixed3D;
        MinimizeBox = false;
        MaximizeBox = false;
        Icon = ui.GetIconResource("App.ico");
        ShowIcon = true;
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
                if (_cts.IsCancellationRequested)
                {
                    DialogResult = DialogResult.Cancel;
                }
                else
                {
                    Exception = ExceptionDispatchInfo.Capture(ex);
                    DialogResult = DialogResult.Abort;
                }
            }
            finally
            {
                _allowClose = true;
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

        if (!_allowClose)
        {
            e.Cancel = true;
            _cts.Cancel();
            _cancelButton.Enabled = false;
        }
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
