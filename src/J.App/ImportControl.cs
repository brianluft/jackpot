using System.Collections.Frozen;
using System.Data;
using J.Core;

namespace J.App;

public sealed class ImportControl : UserControl
{
    private readonly Preferences _preferences;
    private readonly LibraryProviderAdapter _libraryProvider;
    private readonly ImportQueue _queue;
    private readonly Ui _ui;
    private readonly TableLayoutPanel _table;
    private readonly FlowLayoutPanel _inputFlow,
        _convertFlow,
        _leftButtonsFlow,
        _rightButtonsFlow;
    private readonly Label _dragonDropLabel,
        _convertLabel,
        _qualityLabel,
        _speedLabel,
        _audioLabel;
    private readonly ComboBox _convertCombo,
        _qualityCombo,
        _speedCombo,
        _audioCombo;
    private readonly Button _startButton,
        _stopButton,
        _clearButton;
    private readonly DataGridView _grid;
    private readonly DataGridViewColumn _colMessage;
    private string _title = "Import";

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

    public ImportControl(Preferences preferences, LibraryProviderAdapter libraryProvider, ImportQueue importQueue)
    {
        _preferences = preferences;
        _libraryProvider = libraryProvider;
        _queue = importQueue;
        Ui ui = new(this);
        _ui = ui;

        Controls.Add(_table = ui.NewTable(3, 3));
        {
            _table.Padding = ui.DefaultPadding;
            _table.RowStyles[2].SizeType = SizeType.Percent;
            _table.RowStyles[2].Height = 100;
            _table.ColumnStyles[0].SizeType = SizeType.Absolute;
            _table.ColumnStyles[0].Width = ui.GetLength(300);
            _table.ColumnStyles[1].SizeType = SizeType.Percent;
            _table.ColumnStyles[1].Width = 100;
            _table.ColumnStyles[2].SizeType = SizeType.Absolute;
            _table.ColumnStyles[2].Width = ui.GetLength(300);

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
                    }

                    _convertFlow.Controls.Add(
                        ui.NewLabeledPair("Video quality:", _qualityCombo = ui.NewDropDownList(200), out _qualityLabel)
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
                            out _speedLabel
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
                        ui.NewLabeledPair("Audio bitrate:", _audioCombo = ui.NewDropDownList(200), out _audioLabel)
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
                    _dragonDropLabel.AutoSize = false;
                    _dragonDropLabel.Dock = DockStyle.Fill;
                    _dragonDropLabel.TextAlign = ContentAlignment.BottomCenter;
                    _dragonDropLabel.Margin += ui.BottomSpacing;
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

                    _colMessage = _grid.Columns[_grid.Columns.Add("message", "Message")];
                    {
                        _colMessage.DataPropertyName = "message";
                        _colMessage.Width = ui.GetLength(200);
                    }

                    var colFilePath = _grid.Columns[_grid.Columns.Add("filename", "Filename")];
                    {
                        colFilePath.DataPropertyName = "filename";
                        colFilePath.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    }
                }
            }
        }

        _queue.IsRunningChanged += Queue_IsRunningChanged;
        _queue.FileCompleted += Queue_FileCompleted;
        _queue.DataTable.RowChanged += delegate
        {
            UpdateTitle();
        };

        EnableDisableButtons();
    }

    private void Grid_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count || e.ColumnIndex != 0)
            return;

        var row = ((DataRowView)_grid.Rows[e.RowIndex].DataBoundItem!).Row;
        var state = (ImportQueue.FileState)row["state"];
        if (state == ImportQueue.FileState.Failed)
        {
            var message = (string)row["error"];
            MessageBox.Show(message, "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void QualityCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        _preferences.SetText(Preferences.Key.ImportControl_VideoQuality, (string)_qualityCombo.SelectedItem!);
    }

    private void SpeedCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        _preferences.SetText(Preferences.Key.ImportControl_CompressionLevel, (string)_speedCombo.SelectedItem!);
    }

    private void AudioCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        _preferences.SetText(Preferences.Key.ImportControl_AudioBitrate, (string)_audioCombo.SelectedItem!);
    }

    private void Queue_IsRunningChanged(object? sender, EventArgs e)
    {
        BeginInvoke(() =>
        {
            EnableDisableButtons();

            if (_queue.IsRunning)
                Title = "Import (0%)";
            else
                Title = "Import";
        });
    }

    private void Queue_FileCompleted(object? sender, EventArgs e)
    {
        UpdateTitle();
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
        _qualityLabel.Enabled = !running && option.Convert;
        _qualityCombo.Enabled = !running && option.Convert;
        _speedLabel.Enabled = !running && option.Convert;
        _speedCombo.Enabled = !running && option.Convert;
        _audioLabel.Enabled = !running && option.Convert;
        _audioCombo.Enabled = !running && option.Convert;
    }

    private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.ColumnIndex == _colMessage.Index)
        {
            e.Handled = true;
            e.Paint(e.ClipBounds, DataGridViewPaintParts.All ^ DataGridViewPaintParts.ContentForeground);
            var g = e.Graphics!;

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
            g.DrawRectangle(borderPen, rect);

            if (state is ImportQueue.FileState.Working)
            {
                using SolidBrush foregroundBrush = new(MyColors.ProgressBarForeground);
                rect.Inflate(_ui.GetSize(-1, -1));
                rect.Width = (int)(rect.Width * progress);
                g.FillRectangle(foregroundBrush, rect);
            }

            // Then, draw the message on top.
            var textBounds = e.CellBounds;
            textBounds.Inflate(_ui.GetSize(-2, -2));
            textBounds.Height -= _ui.GetLength(1);
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
    }

    private void Grid_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data!.GetDataPresent(DataFormats.FileDrop))
            e.Effect = DragDropEffects.Copy;
    }

    private void Grid_DragDrop(object? sender, DragEventArgs e)
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

        EnableDisableButtons();
    }

    private void StartButton_Click(object? sender, EventArgs e)
    {
        _queue.Start();
    }

    private void StopButton_Click(object? sender, EventArgs e)
    {
        _queue.Stop();
    }

    private void ClearButton_Click(object? sender, EventArgs e)
    {
        _queue.Clear();
        EnableDisableButtons();
    }

    private void ConvertCombo_SelectedValueChanged(object? sender, EventArgs e)
    {
        EnableDisableButtons();
    }

    private readonly record struct ConvertOption(string Display, bool Convert);
}
