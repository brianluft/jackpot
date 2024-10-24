using System.ComponentModel;
using System.Diagnostics;
using Humanizer;

namespace J.App;

public sealed class ImportProgressFormFactory(S3Uploader s3Uploader)
{
    public ImportProgressForm New(long totalBytes, ImportProgressForm.WorkDelegate action)
    {
        return new(totalBytes, action, s3Uploader);
    }
}

public sealed class ImportProgressForm : Form
{
    private readonly Container _components;
    private readonly TableLayoutPanel _table;
    private readonly Label _fileLabel,
        _messageLabel,
        _timeLabel;
    private readonly ProgressBar _progressBar;
    private readonly FlowLayoutPanel _buttonFlow;
    private readonly Button _cancelButton;
    private readonly CancellationTokenSource _cts = new();
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly long _totalBytes;
    private readonly S3Uploader _s3Uploader;
    private readonly long _bytesUploadedAtStart;
    private bool _done;

    public delegate void UpdateFileDelegate(int fileIndex, int numFiles, string filename);

    public delegate void WorkDelegate(UpdateFileDelegate updateFileNumber, CancellationToken cancel);

    public ImportProgressForm(long totalBytes, WorkDelegate action, S3Uploader s3Uploader)
    {
        _totalBytes = (long)(totalBytes * 1.03d);
        _s3Uploader = s3Uploader;
        _bytesUploadedAtStart = _s3Uploader.BytesUploaded;
        Ui ui = new(this);

        _components = new();

        Controls.Add(_table = ui.NewTable(1, 6));
        {
            _table.Padding = ui.DefaultPadding;

            _table.Controls.Add(_fileLabel = ui.NewLabel("File ? of ?\n—"), 0, 1);

            _table.Controls.Add(_progressBar = ui.NewProgressBar(400), 0, 3);
            {
                _progressBar.Margin += ui.TopSpacingBig;
                _progressBar.Maximum = 1000;
            }

            _table.Controls.Add(_messageLabel = ui.NewLabel("Starting."), 0, 2);
            {
                _messageLabel.Margin += ui.TopSpacingBig;
            }

            _table.Controls.Add(_timeLabel = ui.NewLabel("Elapsed:\nRemaining:"), 0, 4);
            {
                _timeLabel.Margin += ui.TopSpacingBig;
            }

            _table.Controls.Add(_buttonFlow = ui.NewFlowRow(), 0, 5);
            {
                _buttonFlow.Margin += ui.TopSpacingBig;
                _buttonFlow.Dock = DockStyle.Right;

                _buttonFlow.Controls.Add(_cancelButton = ui.NewButton("Cancel"));
                {
                    _cancelButton.Click += CancelButton_Click;
                }
            }
        }

        _timer = new(_components) { Interval = 250, Enabled = true };
        {
            _timer.Tick += Timer_Tick;
        }

        Text = "Progress";
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        FormBorderStyle = FormBorderStyle.Fixed3D;
        MinimizeBox = true;
        MaximizeBox = false;
        CancelButton = _cancelButton;
        Icon = ui.GetIconResource("App.ico");
        ShowIcon = true;
        ShowInTaskbar = true;

        Shown += async delegate
        {
            try
            {
                await Task.Run(() => action(UpdateFile, _cts.Token)).ConfigureAwait(true);
                DialogResult = DialogResult.OK;
            }
            catch (OperationCanceledException)
            {
                DialogResult = DialogResult.Cancel;
            }
            catch (Exception ex)
            {
                _progressBar.Enabled = false;
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                DialogResult = DialogResult.Abort;
            }
            finally
            {
                _done = true;
                Close();
            }
        };
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_done)
        {
            var response = MessageBox.Show(
                this,
                "Are you sure you want to cancel this import?",
                "Jackpot",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2
            );
            if (response != DialogResult.OK)
            {
                e.Cancel = true;
                return;
            }
        }

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
        _done = true;
        base.Dispose(disposing);

        if (disposing)
        {
            _cts.Dispose();
            _components.Dispose();
        }
    }

    public void UpdateFile(int fileIndex, int numFiles, string filename)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => UpdateFile(fileIndex, numFiles, filename));
            return;
        }

        if (filename.Length > 60)
            filename = filename[..60] + "...";

        _fileLabel.Text = $"File {fileIndex + 1:#,##0} of {numFiles:#,##0}\n{EscapeLabelText(filename)}";
    }

    private static string EscapeLabelText(string x) => x.Replace("&", "&&");

    private void Timer_Tick(object? sender, EventArgs e)
    {
        var uploadedBytes = _s3Uploader.BytesUploaded - _bytesUploadedAtStart;
        var hasStarted = uploadedBytes > 0;
        if (!hasStarted)
            _stopwatch.Restart();

        var percent = _totalBytes == 0 ? 0d : (double)uploadedBytes / _totalBytes;
        _progressBar.Value = Math.Clamp((int)(percent * 1000), 0, 1000);
        var elapsedTime = _stopwatch.Elapsed;
        var bytesPerSecond = elapsedTime.TotalSeconds == 0 ? 0 : uploadedBytes / elapsedTime.TotalSeconds;
        var megabitsPerSecond = bytesPerSecond * 8 / 1_000_000;
        if (uploadedBytes > _totalBytes)
        {
            _messageLabel.Text = $"Uploaded {uploadedBytes / 1_000_000:#,##0} MB ({megabitsPerSecond:#,##0} Mbps)";
        }
        else
        {
            _messageLabel.Text =
                $"Uploaded {uploadedBytes / 1_000_000:#,##0} MB of {_totalBytes / 1_000_000:#,##0} MB ({megabitsPerSecond:#,##0} Mbps)";
        }

        TimeSpan? remainingTime = null;
        if (hasStarted && bytesPerSecond > 0)
        {
            var remainingBytes = _totalBytes - uploadedBytes;
            remainingTime = TimeSpan.FromSeconds(remainingBytes / bytesPerSecond);
        }

        if (!hasStarted)
            _timeLabel.Text = $"Elapsed:\nRemaining:";
        else if (remainingTime.HasValue && uploadedBytes < _totalBytes)
            _timeLabel.Text = $"Elapsed: {Humanize(elapsedTime)}\nRemaining: {Humanize(remainingTime.Value)}";
        else
            _timeLabel.Text = $"Elapsed: {Humanize(elapsedTime)}\nRemaining:";
    }

    private static string Humanize(TimeSpan x) => x.Humanize(2, minUnit: Humanizer.Localisation.TimeUnit.Second);
}
