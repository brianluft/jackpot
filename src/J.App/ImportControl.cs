using System.Data;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using Humanizer;
using J.Core;

namespace J.App;

public sealed class ImportControl : UserControl
{
    private const double FPS = 60;
    private const string DEFAULT_STATS_TEXT = "Uploaded:\nElapsed:\nRemaining:";
    private readonly Preferences _preferences;
    private readonly ImportQueue _queue;
    private readonly S3Uploader _s3Uploader;
    private readonly Ui _ui;
    private readonly TableLayoutPanel _table;
    private readonly FlowLayoutPanel _inputFlow,
        _convertFlow,
        _leftButtonsFlow,
        _rightButtonsFlow;
    private readonly MyLabel _dragonDropLabel,
        _convertLabel,
        _convertQualityLabel,
        _convertSpeedLabel,
        _convertAudioLabel,
        _statsLabel;
    private readonly ComboBox _convertCombo,
        _qualityCombo,
        _speedCombo,
        _audioCombo;
    private readonly Button _startButton,
        _stopButton,
        _clearButton;
    private readonly DataGridView _grid;
    private readonly DataGridViewColumn _colMessage,
        _colSize;
    private readonly System.Windows.Forms.Timer _animationTimer,
        _statsTimer;
    private readonly Stopwatch _animationStopwatch = Stopwatch.StartNew();
    private Stopwatch? _startStopwatch;
    private string _title = "Import";

    private readonly Stopwatch _uploadedBytesMonotonicStopwatch = Stopwatch.StartNew();
    private long _uploadedBytesMonotonic;

#pragma warning disable IDE0044 // Add readonly modifier
    // Changes at the end of the constructor. Technically readonly, but that's confusing to read.
    private bool _initializing = true;
#pragma warning restore IDE0044 // Add readonly modifier

    public string Title
    {
        get => _title;
        private set
        {
            _title = value;
            TitleChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public event EventHandler? TitleChanged;

    public bool ImportInProgress => _queue.IsRunning;

    public ImportControl(Preferences preferences, ImportQueue importQueue, S3Uploader s3Uploader)
    {
        _preferences = preferences;
        _queue = importQueue;
        _s3Uploader = s3Uploader;
        Ui ui = new(this);
        _ui = ui;

        Controls.Add(_table = ui.NewTable(3, 4));
        {
            _table.Padding = ui.DefaultPadding;
            _table.RowStyles[2].SizeType = SizeType.Percent;
            _table.RowStyles[2].Height = 100;
            _table.ColumnStyles[1].SizeType = SizeType.Percent;
            _table.ColumnStyles[1].Width = 100;

            _table.Controls.Add(_inputFlow = ui.NewFlowColumn(), 0, 0);
            {
                _table.SetColumnSpan(_inputFlow, 3);

                _inputFlow.Controls.Add(_convertFlow = ui.NewFlowRow());
                {
                    _convertFlow.Controls.Add(
                        ui.NewLabeledPair(
                            "For incompatible video formats:",
                            _convertCombo = ui.NewDropDownList(225),
                            out _convertLabel
                        )
                    );
                    {
                        _convertFlow.Margin += ui.BottomSpacing;

                        _convertCombo.DisplayMember = "Display";
                        _convertCombo.ValueMember = "Value";
                        _convertCombo.Items.Add(new ConvertOption("Convert to MP4 (H.264)", true));
                        _convertCombo.Items.Add(new ConvertOption("Skip", false));
                        _convertCombo.SelectedIndex = 0;
                        _convertCombo.Margin += ui.RightSpacing;
                        _convertCombo.SelectedValueChanged += ConvertCombo_SelectedValueChanged;

                        var autoconvert = preferences.GetBoolean(Preferences.Key.ImportControl_AutoConvert);
                        _convertCombo.SelectedIndex = autoconvert ? 0 : 1;
                    }

                    _convertFlow.Controls.Add(
                        ui.NewLabeledPair(
                            "Video quality:",
                            _qualityCombo = ui.NewDropDownList(200),
                            out _convertQualityLabel
                        )
                    );
                    {
                        _qualityCombo.Margin += ui.RightSpacing;
                        _qualityCombo.SelectedIndexChanged += QualityCombo_SelectedIndexChanged;

                        List<string> qualities = [];
                        for (var i = 0; i <= 28; i++)
                            qualities.Add(i.ToString());

                        qualities[0] = "0 (best quality)";
                        qualities[17] = "17 (recommended)";
                        qualities[28] = "28 (worst quality)";
                        qualities.Reverse();

                        foreach (var quality in qualities)
                            _qualityCombo.Items.Add(quality);

                        var index = qualities.IndexOf(preferences.GetText(Preferences.Key.ImportControl_VideoQuality));
                        if (index >= 0)
                            _qualityCombo.SelectedIndex = index;
                    }

                    _convertFlow.Controls.Add(
                        ui.NewLabeledPair(
                            "Video compression level:",
                            _speedCombo = ui.NewDropDownList(200),
                            out _convertSpeedLabel
                        )
                    );
                    {
                        _speedCombo.Margin += ui.RightSpacing;
                        _speedCombo.SelectedIndexChanged += SpeedCombo_SelectedIndexChanged;

                        List<string> speeds =
                        [
                            "ultrafast (worst compression)",
                            "superfast",
                            "veryfast",
                            "faster",
                            "fast",
                            "medium",
                            "slow (recommended)",
                            "slower",
                            "veryslow (best compression)",
                        ];
                        foreach (var speed in speeds)
                            _speedCombo.Items.Add(speed);

                        var index = speeds.IndexOf(preferences.GetText(Preferences.Key.ImportControl_CompressionLevel));
                        if (index >= 0)
                            _speedCombo.SelectedIndex = index;
                    }

                    _convertFlow.Controls.Add(
                        ui.NewLabeledPair(
                            "Audio bitrate:",
                            _audioCombo = ui.NewDropDownList(200),
                            out _convertAudioLabel
                        )
                    );
                    {
                        _audioCombo.SelectedIndexChanged += AudioCombo_SelectedIndexChanged;

                        List<string> bitrates =
                        [
                            "96 kbps (worst quality)",
                            "128 kbps",
                            "160 kbps",
                            "192 kbps",
                            "256 kbps (recommended)",
                            "320 kbps (best quality)",
                        ];
                        foreach (var speed in bitrates)
                            _audioCombo.Items.Add(speed);

                        var index = bitrates.IndexOf(preferences.GetText(Preferences.Key.ImportControl_AudioBitrate));
                        if (index >= 0)
                            _audioCombo.SelectedIndex = index;
                    }
                }

                _table.Controls.Add(_leftButtonsFlow = ui.NewFlowRow(), 0, 1);
                {
                    _leftButtonsFlow.Controls.Add(_startButton = ui.NewButton("Start import"));
                    {
                        _startButton.Margin += ui.RightSpacing;
                        _startButton.Click += StartButton_Click;
                    }

                    _leftButtonsFlow.Controls.Add(_stopButton = ui.NewButton("Stop"));
                    {
                        _stopButton.Margin += ui.RightSpacing;
                        _stopButton.Click += StopButton_Click;
                    }
                }

                _table.Controls.Add(_dragonDropLabel = ui.NewLabel("Drag-and-drop movie files below."), 1, 1);
                {
                    _dragonDropLabel.Dock = DockStyle.Fill;
                    _dragonDropLabel.TextAlign = ContentAlignment.MiddleLeft;
                }

                _table.Controls.Add(_rightButtonsFlow = ui.NewFlowRow(), 2, 1);
                {
                    _rightButtonsFlow.Dock = DockStyle.Right;

                    _rightButtonsFlow.Controls.Add(_clearButton = ui.NewButton("Clear list"));
                    {
                        _clearButton.Click += ClearButton_Click;
                    }
                }

                _table.Controls.Add(_grid = ui.NewDataGridView(), 0, 2);
                {
                    _table.SetColumnSpan(_grid, 3);
                    _grid.Margin += ui.TopSpacing;
                    _grid.AllowDrop = true;
                    _grid.DragEnter += Grid_DragEnter;
                    _grid.DragDrop += Grid_DragDrop;
                    _grid.DataSource = _queue.DataTable;
                    _grid.ColumnHeadersVisible = false;
                    _grid.CellPainting += Grid_CellPainting;
                    _grid.CellClick += Grid_CellClick;
                    _grid.CellBorderStyle = DataGridViewCellBorderStyle.None;
                    _grid.AllowUserToResizeColumns = false;

                    _colMessage = _grid.Columns[_grid.Columns.Add("message", "Message")];
                    {
                        _colMessage.DataPropertyName = "message";
                        _colMessage.Width = ui.GetLength(225);
                    }

                    var colFilePath = _grid.Columns[_grid.Columns.Add("filename", "Filename")];
                    {
                        colFilePath.DataPropertyName = "filename";
                        colFilePath.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    }

                    _colSize = _grid.Columns[_grid.Columns.Add("size_mb", "Size")];
                    {
                        _colSize.DataPropertyName = "size_mb";
                        _colSize.Width = ui.GetLength(100);
                        _colSize.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

                        DataGridViewCellStyle style = new(_colSize.DefaultCellStyle) { Format = "#,##0 MB" };
                        _colSize.DefaultCellStyle = style;
                    }
                }

                _table.Controls.Add(_statsLabel = ui.NewLabel(DEFAULT_STATS_TEXT), 0, 3);
                {
                    _table.SetColumnSpan(_statsLabel, 3);
                    _statsLabel.Margin += ui.TopSpacing;
                }
            }
        }

        _animationTimer = new() { Interval = (int)(1000 / FPS), Enabled = true };
        _animationTimer.Tick += AnimationTimer_Tick;

        _statsTimer = new() { Interval = 1000, Enabled = true };
        _statsTimer.Tick += StatsTimer_Tick;

        _queue.IsRunningChanged += Queue_IsRunningChanged;
        _queue.FileCompleted += Queue_FileCompleted;
        _queue.DataTable.RowChanged += delegate
        {
            ShowMessageBoxOnException(UpdateTitle);
        };

        _initializing = false;
        EnableDisableButtons();
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            _grid.InvalidateColumn(_colMessage.Index);
        }
        catch { }
    }

    private void StatsTimer_Tick(object? sender, EventArgs e)
    {
        if (_startStopwatch is null)
        {
            if (_statsLabel.Text != DEFAULT_STATS_TEXT)
                _statsLabel.Text = DEFAULT_STATS_TEXT;

            if (_statsLabel.Enabled)
                _statsLabel.Enabled = false;

            return;
        }

        var lastBytes = _uploadedBytesMonotonic;
        var duration = _uploadedBytesMonotonicStopwatch.Elapsed;

        var currentBytes = _s3Uploader.UploadedBytesMonotonic;
        _uploadedBytesMonotonicStopwatch.Restart();
        _uploadedBytesMonotonic = currentBytes;

        var bytes = currentBytes - lastBytes;
        var bytesPerSecond = duration.TotalSeconds == 0 ? double.NaN : (bytes / duration.TotalSeconds);
        var megabitsPerSecond = bytesPerSecond * 8 / 1_000_000;
        var mbpsText = double.IsNaN(megabitsPerSecond) ? "" : $" ({megabitsPerSecond:#,##0} Mbps)";
        var elapsed = _startStopwatch.Elapsed;

        var mibWithRollback = (double)_s3Uploader.UploadedBytesWithRollbacks / 1024 / 1024;
        double mibTotal = 0;
        foreach (DataRow x in _queue.DataTable.Rows)
            mibTotal += (double)x["size_mb"];

        string remainingStr = "\u2014";
        var mibRemaining = mibTotal - mibWithRollback;
        if (mibRemaining > 0 && elapsed.TotalSeconds > 0)
        {
            var averageMibPerSecond = mibWithRollback / elapsed.TotalSeconds;
            if (averageMibPerSecond > 0)
            {
                var remaining = TimeSpan.FromSeconds(mibRemaining / averageMibPerSecond);
                remainingStr = remaining.Humanize(2, minUnit: Humanizer.Localisation.TimeUnit.Second);
            }
        }

        if (!_statsLabel.Enabled)
            _statsLabel.Enabled = true;

        var uploadedTotalStr = mibWithRollback < mibTotal ? $" of {mibTotal:#,##0} MB" : "";

        _statsLabel.Text = $"""
            Uploaded: {mibWithRollback:#,##0} MB{uploadedTotalStr}{mbpsText}
            Elapsed: {elapsed.Humanize(2, minUnit: Humanizer.Localisation.TimeUnit.Second)}
            Remaining: {remainingStr}
            """;
    }

    private void Grid_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        ShowMessageBoxOnException(() =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count || e.ColumnIndex != 0)
                return;

            var row = ((DataRowView)_grid.Rows[e.RowIndex].DataBoundItem!).Row;
            var state = (ImportQueue.FileState)row["state"];
            if (state == ImportQueue.FileState.Failed)
            {
                var message = (string)row["error"];
                MessageForm.Show(FindForm()!, message, "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        });
    }

    private void ConvertCombo_SelectedValueChanged(object? sender, EventArgs e)
    {
        if (_initializing)
            return;

        ShowMessageBoxOnException(() =>
        {
            var autoconvert = ((ConvertOption)_convertCombo.SelectedItem!).Convert;
            _preferences.SetBoolean(Preferences.Key.ImportControl_AutoConvert, autoconvert);
            EnableDisableButtons();
        });
    }

    private void QualityCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_initializing)
            return;

        ShowMessageBoxOnException(
            () => _preferences.SetText(Preferences.Key.ImportControl_VideoQuality, (string)_qualityCombo.SelectedItem!)
        );
    }

    private void SpeedCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_initializing)
            return;

        ShowMessageBoxOnException(
            () =>
                _preferences.SetText(Preferences.Key.ImportControl_CompressionLevel, (string)_speedCombo.SelectedItem!)
        );
    }

    private void AudioCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_initializing)
            return;

        ShowMessageBoxOnException(
            () => _preferences.SetText(Preferences.Key.ImportControl_AudioBitrate, (string)_audioCombo.SelectedItem!)
        );
    }

    private void Queue_IsRunningChanged(object? sender, EventArgs e)
    {
        BeginInvoke(() =>
        {
            try
            {
                EnableDisableButtons();

                if (_queue.IsRunning)
                {
                    Title = "Import (0%)";
                    _startStopwatch = Stopwatch.StartNew();
                }
                else
                {
                    Title = "Import";
                    _startStopwatch = null;
                }
            }
            catch { }
        });
    }

    private void Queue_FileCompleted(object? sender, EventArgs e)
    {
        ShowMessageBoxOnException(UpdateTitle);
    }

    private void UpdateTitle()
    {
        int numFiles,
            numCompleted;
        lock (_queue.DataTableLock)
        {
            numFiles = _queue.DataTable.Rows.Count;
            numCompleted = _queue
                .DataTable.Rows.Cast<DataRow>()
                .Count(row =>
                    (ImportQueue.FileState)row["state"] is ImportQueue.FileState.Success or ImportQueue.FileState.Failed
                );
        }

        BeginInvoke(() =>
        {
            Title = $"Import ({numCompleted * 100 / numFiles}%)";
        });
    }

    private void EnableDisableButtons()
    {
        var any = _queue.Count > 0;
        var running = _queue.IsRunning;
        _startButton.Enabled = any && !running;
        _stopButton.Enabled = running;
        _clearButton.Enabled = any && !running;

        var option = (ConvertOption)_convertCombo.SelectedItem!;
        _convertLabel.Enabled = !running;
        _convertCombo.Enabled = !running;
        _convertQualityLabel.Enabled = !running && option.Convert;
        _qualityCombo.Enabled = !running && option.Convert;
        _convertSpeedLabel.Enabled = !running && option.Convert;
        _speedCombo.Enabled = !running && option.Convert;
        _convertAudioLabel.Enabled = !running && option.Convert;
        _audioCombo.Enabled = !running && option.Convert;
    }

    private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        try
        {
            if (e.ColumnIndex == _colMessage.Index)
                PaintMessageColumn(e);
        }
        catch { }
    }

    private void PaintMessageColumn(DataGridViewCellPaintingEventArgs e)
    {
        e.Handled = true;
        var g = e.Graphics!;
        e.Paint(e.ClipBounds, DataGridViewPaintParts.All ^ DataGridViewPaintParts.ContentForeground);

        var row = ((DataRowView)_grid.Rows[e.RowIndex].DataBoundItem!).Row;
        var message = (string)row["message"];
        var progress = (double)row["progress"];
        var state = (ImportQueue.FileState)row["state"];

        // Draw the progress as a progressbar behind.
        var rect = e.CellBounds;
        rect.Inflate(_ui.GetSize(-4, -4));
        using Pen borderPen = new(MyColors.ProgressBarBorder, _ui.GetLength(1));
        var backgroundColor = state switch
        {
            ImportQueue.FileState.Failed => MyColors.ProgressBarFailedBackground,
            ImportQueue.FileState.Success => MyColors.ProgressBarSuccessBackground,
            _ => MyColors.ProgressBarBackground,
        };
        using SolidBrush backgroundBrush = new(backgroundColor);
        g.FillRectangle(backgroundBrush, rect);

        if (state is ImportQueue.FileState.Working)
        {
            if (progress == 0)
            {
                DrawBarberPole(e, rect);
            }
            else if (progress > 0)
            {
                using SolidBrush foregroundBrush = new(MyColors.ProgressBarForeground);
                var progressRect = rect;
                progressRect.Inflate(_ui.GetSize(-1, -1));
                g.SetClip(progressRect);
                progressRect.Width = (int)(rect.Width * Math.Clamp(progress, 0, 1));
                g.FillRectangle(foregroundBrush, progressRect);
                g.ResetClip();
            }
        }

        g.DrawRectangle(borderPen, rect);

        // Then, draw the message on top.
        var textBounds = e.CellBounds;
        textBounds.Inflate(_ui.GetSize(-2, -2));
        textBounds.Y -= _ui.GetLength(1);
        TextRenderer.DrawText(
            g,
            message,
            Font,
            textBounds,
            Color.White,
            Color.Transparent,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter
        );
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        if (Visible)
            _animationTimer.Start();
        else
            _animationTimer.Stop();
    }

    private void DrawBarberPole(DataGridViewCellPaintingEventArgs e, Rectangle rect)
    {
        Debug.Assert(e.Graphics is not null);

        // Get DPI scaling factor
        float dpiScale = DeviceDpi / 96f;

        // Define animation parameters with DPI scaling
        float stripeWidth = 45 * dpiScale; // Width of each color stripe
        float animationSpeed = 50f * dpiScale; // Pixels per second

        // Calculate animation offset based on time
        float timeOffset = (float)(_animationStopwatch.ElapsedMilliseconds / 1000.0);
        float xOffset = (timeOffset * animationSpeed) % (2 * stripeWidth);

        // Create path for clipping
        using var path = new GraphicsPath();
        path.AddRectangle(rect);
        e.Graphics.SetClip(path);

        // Paint cell background
        using var backgroundBrush = new SolidBrush(MyColors.ProgressBarBackground);
        e.Graphics.FillRectangle(backgroundBrush, rect);

        // Calculate number of stripes needed to cover cell
        int totalWidth = rect.Width + (int)(stripeWidth * 2);
        int numStripes = (totalWidth / (int)stripeWidth) + 2;

        // Draw each stripe as a filled polygon
        var oldSmoothingMode = e.Graphics.SmoothingMode;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var edgeCurvatureHeight = 3 * dpiScale;
        var edgeCurvatureWidth = 10 * dpiScale;

        for (int i = -1; i < numStripes; i += 2)
        {
            float x1 = rect.Left + (i * stripeWidth) - xOffset;
            float x0 = x1 - edgeCurvatureWidth;
            float x2 = x1 + edgeCurvatureWidth;

            float x4 = x1 + stripeWidth;
            float x3 = x4 - edgeCurvatureWidth;
            float x5 = x4 + edgeCurvatureWidth;

            float y0 = rect.Top;
            float y1 = rect.Top + edgeCurvatureHeight;
            float y2 = rect.Bottom - edgeCurvatureHeight;
            float y3 = rect.Bottom;

            Point TopPoint(float x, float y) => new((int)x, (int)y);
            Point BottomPoint(float x, float y) => new((int)(x + stripeWidth / 2), (int)y);

            // Create points for trapezoid
            Point[] points =
            [
                TopPoint(x0, y0),
                TopPoint(x3, y0),
                TopPoint(x4, y1),
                BottomPoint(x4, y2),
                BottomPoint(x5, y3),
                BottomPoint(x2, y3),
                BottomPoint(x1, y2),
                TopPoint(x1, y1),
            ];

            // Fill trapezoid with appropriate color
            using var brush = new SolidBrush(MyColors.BarberPoleStripe);
            e.Graphics.FillPolygon(brush, points);
        }

        e.Graphics.SmoothingMode = oldSmoothingMode;
        e.Graphics.ResetClip();
    }

    private void Grid_DragEnter(object? sender, DragEventArgs e)
    {
        try
        {
            if (e.Data!.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }
        catch { }
    }

    private void Grid_DragDrop(object? sender, DragEventArgs e)
    {
        ShowMessageBoxOnException(() =>
        {
            var files = (string[])e.Data!.GetData(DataFormats.FileDrop)!;

            List<string> filePaths = [];
            foreach (var path in files)
            {
                if (Directory.Exists(path))
                {
                    filePaths.AddRange(Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories));
                }
                else if (File.Exists(path))
                {
                    filePaths.Add(path);
                }
            }

            _queue.AddFiles(filePaths);

            _grid.ClearSelection();
            EnableDisableButtons();
        });
    }

    private void StartButton_Click(object? sender, EventArgs e)
    {
        ShowMessageBoxOnException(_queue.Start);
    }

    private void StopButton_Click(object? sender, EventArgs e)
    {
        ShowMessageBoxOnException(_queue.Stop);
    }

    private void ClearButton_Click(object? sender, EventArgs e)
    {
        ShowMessageBoxOnException(() =>
        {
            _queue.Clear();
            EnableDisableButtons();
        });
    }

    private readonly record struct ConvertOption(string Display, bool Convert);

    private bool ShowMessageBoxOnException(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            MessageForm.Show(this, ex.Message, "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }
}
