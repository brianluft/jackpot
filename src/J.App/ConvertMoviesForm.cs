using J.Core;

namespace J.App;

public sealed class ConvertMoviesForm : Form
{
    private readonly TableLayoutPanel _table;
    private readonly FlowLayoutPanel _topFlow1,
        _topFlow2,
        _topFlow3,
        _bottomFlow,
        _listFlow;
    private readonly Label _helpLabel;
    private readonly ListBox _filesList;
    private readonly LinkLabel _addLink,
        _removeLink;
    private readonly TextBox _outputDirText;
    private readonly ComboBox _qualityCombo,
        _speedCombo,
        _audioCombo;
    private readonly Button _okButton,
        _cancelButton;
    private readonly Preferences _preferences;

    public ConvertMoviesForm(Preferences preferences)
    {
        _preferences = preferences;
        Ui ui = new(this);
        Control p;

        Controls.Add(_table = ui.NewTable(2, 6));
        {
            _table.Padding = ui.DefaultPadding;
            _table.RowStyles[1].SizeType = SizeType.Percent;
            _table.RowStyles[1].Height = 100;

            _table.Controls.Add(_helpLabel = ui.NewLabel("Drag-and-drop movie files below."), 0, 0);
            {
                _helpLabel.Padding += ui.GetPadding(0, 0, 0, 2);
            }

            _table.Controls.Add(_listFlow = ui.NewFlowRow(), 1, 0);
            {
                _listFlow.Dock = DockStyle.Right;

                _listFlow.Controls.Add(_addLink = ui.NewLinkLabel("Add..."));
                {
                    _addLink.Click += AddLink_Click;
                }

                _listFlow.Controls.Add(_removeLink = ui.NewLinkLabel("Remove"));
                {
                    _removeLink.Click += RemoveLink_Click;
                    _removeLink.Enabled = false;
                    _removeLink.Margin += ui.LeftSpacing;
                }
            }

            _table.Controls.Add(_filesList = ui.NewListBox(), 0, 1);
            {
                _table.SetColumnSpan(_filesList, 2);
                _filesList.SelectionMode = SelectionMode.MultiExtended;
                _filesList.Margin += ui.BottomSpacingBig;
                _filesList.AllowDrop = true;
                _filesList.DragEnter += FilesList_DragEnter;
                _filesList.DragDrop += FilesList_DragDrop;
                _filesList.SelectedIndexChanged += FilesList_SelectedIndexChanged;
            }

            _table.Controls.Add(_topFlow1 = ui.NewFlowRow(), 0, 2);
            {
                _table.SetColumnSpan(_topFlow1, 2);
                _topFlow1.Padding += ui.BottomSpacing;

                _topFlow1.Controls.Add(ui.NewLabeledPair("Video quality:", _qualityCombo = ui.NewDropDown(200)));
                {
                    _qualityCombo.Margin += ui.RightSpacing;

                    List<string> qualities = [];
                    for (var i = 0; i <= 28; i++)
                        qualities.Add(i.ToString());

                    qualities[0] = "0 (best quality)";
                    qualities[17] = "17 (recommended)";
                    qualities[28] = "28 (worst quality)";
                    qualities.Reverse();

                    foreach (var quality in qualities)
                        _qualityCombo.Items.Add(quality);

                    var index = qualities.IndexOf(preferences.GetText(Preferences.Key.ConvertMoviesForm_VideoQuality));
                    if (index >= 0)
                        _qualityCombo.SelectedIndex = index;
                }

                _topFlow1.Controls.Add(ui.NewLabeledPair("Compression level:", _speedCombo = ui.NewDropDown(200)));
                {
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

                    var index = speeds.IndexOf(preferences.GetText(Preferences.Key.ConvertMoviesForm_CompressionLevel));
                    if (index >= 0)
                        _speedCombo.SelectedIndex = index;
                }
            }

            _table.Controls.Add(_topFlow2 = ui.NewFlowRow(), 0, 3);
            {
                _table.SetColumnSpan(_topFlow2, 2);
                _topFlow2.Padding += ui.BottomSpacing;

                _topFlow2.Controls.Add(ui.NewLabeledPair("Audio bitrate:", _audioCombo = ui.NewDropDown(200)));
                {
                    List<string> speeds =
                    [
                        "96 kbps (worst quality)",
                        "128 kbps",
                        "160 kbps",
                        "192 kbps",
                        "256 kbps (recommended)",
                        "320 kbps (best quality)",
                    ];
                    foreach (var speed in speeds)
                        _audioCombo.Items.Add(speed);

                    var index = speeds.IndexOf(preferences.GetText(Preferences.Key.ConvertMoviesForm_AudioBitrate));
                    if (index >= 0)
                        _audioCombo.SelectedIndex = index;
                }
            }

            _table.Controls.Add(_topFlow3 = ui.NewFlowRow(), 0, 4);
            {
                _table.SetColumnSpan(_topFlow3, 2);
                _topFlow3.Padding += ui.BottomSpacingBig;

                (p, _outputDirText) = ui.NewLabeledOpenFolderTextBox(
                    "&Output folder:",
                    400,
                    d =>
                    {
                        d.ShowNewFolderButton = true;
                    }
                );
                {
                    _topFlow3.Controls.Add(p);
                    _outputDirText.Text = preferences.GetText(Preferences.Key.ConvertMoviesForm_OutputDirectory);
                }
            }

            _table.Controls.Add(_bottomFlow = ui.NewFlowRow(), 0, 5);
            {
                _table.SetColumnSpan(_bottomFlow, 2);
                _bottomFlow.Dock = DockStyle.Right;

                _bottomFlow.Controls.Add(_okButton = ui.NewButton("Start"));
                {
                    _okButton.Click += OkButton_Click;
                }

                _bottomFlow.Controls.Add(_cancelButton = ui.NewButton("Close"));
                {
                    _cancelButton.Click += delegate
                    {
                        Close();
                    };
                }
            }
        }

        Text = "Convert Movies to MP4";
        StartPosition = FormStartPosition.CenterScreen;
        Size = ui.GetSize(500, 500);
        MinimumSize = ui.GetSize(500, 400);
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = false;
        AcceptButton = _okButton;
        CancelButton = _cancelButton;
        Icon = ui.GetIconResource("App.ico");
        ShowIcon = true;
        ShowInTaskbar = true;
    }

    private void FilesList_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data!.GetDataPresent(DataFormats.FileDrop))
            e.Effect = DragDropEffects.Copy;
    }

    private void FilesList_DragDrop(object? sender, DragEventArgs e)
    {
        var files = (string[])e.Data!.GetData(DataFormats.FileDrop)!;

        foreach (var file in files)
        {
            if (new DirectoryInfo(file).Exists)
            {
                AddDirectory(file);
            }
            else if (new FileInfo(file).Exists)
            {
                if (!_filesList.Items.Contains(file))
                    _filesList.Items.Add(file);
            }
        }

        void AddDirectory(string dir)
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
            {
                if (!_filesList.Items.Contains(file))
                    _filesList.Items.Add(file);
            }
        }
    }

    private void OkButton_Click(object? sender, EventArgs e)
    {
        try
        {
            _preferences.WithTransaction(() =>
            {
                _preferences.SetText(
                    Preferences.Key.ConvertMoviesForm_VideoQuality,
                    (string)_qualityCombo.SelectedItem!
                );
                _preferences.SetText(
                    Preferences.Key.ConvertMoviesForm_CompressionLevel,
                    (string)_speedCombo.SelectedItem!
                );
                _preferences.SetText(Preferences.Key.ConvertMoviesForm_AudioBitrate, (string)_audioCombo.SelectedItem!);
                _preferences.SetText(Preferences.Key.ConvertMoviesForm_OutputDirectory, _outputDirText.Text);
            });

            var outputDir = _outputDirText.Text;
            if (!Directory.Exists(outputDir))
                throw new Exception("Please choose an output folder.");

            var files = _filesList.Items.Cast<string>().ToList();
            if (files.Count == 0)
                throw new Exception("Please add some movies to convert.");

            var videoCrf = int.Parse(((string)_qualityCombo.SelectedItem!).Split(' ')[0]);
            var videoPreset = ((string)_speedCombo.SelectedItem!).Split(' ')[0];
            var audioBitrate = int.Parse(((string)_audioCombo.SelectedItem!).Split(' ')[0]);

            using SimpleProgressForm f =
                new(
                    (updateProgress, updateMessage, cancel) =>
                    {
                        ConvertAll(
                            outputDir,
                            files,
                            videoCrf,
                            videoPreset,
                            audioBitrate,
                            updateProgress,
                            updateMessage,
                            cancel
                        );
                    }
                );
            var result = f.ShowDialog(this);
            if (result == DialogResult.Abort)
                f.Exception!.Throw();
            else if (result == DialogResult.Cancel)
                return;

            Close();
        }
        catch (AggregateException ex)
        {
            MessageBox.Show(ex.InnerExceptions[0].Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ConvertAll(
        string outputDir,
        List<string> sourceFiles,
        int videoCrf,
        string videoPreset,
        int audioBitrate,
        Action<double> updateProgress,
        Action<string> updateMessage,
        CancellationToken cancel
    )
    {
        long totalFrames = 0;
        for (var i = 0; i < sourceFiles.Count; i++)
        {
            cancel.ThrowIfCancellationRequested();
            var sourceFile = sourceFiles[i];

            updateMessage(
                $"Inspecting file {i + 1:#,##0} of {sourceFiles.Count:#,##0}...\n{GetShortenedFilename(sourceFile)}"
            );
            var frames = GetFrameCount(sourceFile, cancel);
            Interlocked.Add(ref totalFrames, frames);
        }

        cancel.ThrowIfCancellationRequested();

        var currentFrame = 0L;

        for (var i = 0; i < sourceFiles.Count; i++)
        {
            cancel.ThrowIfCancellationRequested();
            var sourceFile = sourceFiles[i];

            var baseMessage =
                $"Converting file {i + 1:#,##0} of {sourceFiles.Count:#,##0}...\n{GetShortenedFilename(sourceFile)}\nSpeed: ";
            updateMessage(baseMessage + "N/A");

            var startFrame = currentFrame;

            ConvertOne(
                outputDir,
                sourceFile,
                videoCrf,
                videoPreset,
                audioBitrate,
                frame =>
                {
                    currentFrame = startFrame + frame - 1;
                    updateProgress((double)currentFrame / totalFrames);
                },
                speed => updateMessage(baseMessage + speed),
                cancel
            );

            cancel.ThrowIfCancellationRequested();

            // Remove this file from the listbox.
            Invoke(() =>
            {
                _filesList.Items.Remove(sourceFile);
            });
        }
    }

    private static string GetShortenedFilename(string sourceFile)
    {
        var sourceFileShort = Path.GetFileName(sourceFile);
        if (sourceFileShort.Length > 40)
            sourceFileShort = $"{sourceFileShort[..40]}...";
        return sourceFileShort;
    }

    private void ConvertOne(
        string outputDir,
        string sourceFile,
        int videoCrf,
        string videoPreset,
        int audioBitrate,
        Action<int> updateFrame,
        Action<string> updateSpeed,
        CancellationToken cancel
    )
    {
        var outputFilePath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(sourceFile) + ".mp4");

        var arguments =
            $"-i \"{sourceFile}\" -c:v libx264 -preset \"{videoPreset}\" -crf \"{videoCrf}\" -pix_fmt yuv420p -c:a aac -b:a {audioBitrate}k -threads {Environment.ProcessorCount - 1} -movflags +faststart -hide_banner -loglevel error -progress pipe:1 -y \"{outputFilePath}\"";

        var (exitCode, log) = Ffmpeg.Run(
            arguments,
            output =>
            {
                if (output.StartsWith("frame="))
                {
                    var frame = int.Parse(output.Split('=')[1].Trim());
                    updateFrame(frame);
                }
                else if (output.StartsWith("speed="))
                {
                    updateSpeed(output.Split("=")[1].Trim());
                }
            },
            cancel
        );

        cancel.ThrowIfCancellationRequested();

        if (exitCode != 0)
            throw new Exception(
                $"Failed to convert \"{Path.GetFileName(sourceFile)}\". FFmpeg failed with exit code {exitCode}.\n\nFFmpeg output:\n{log}"
            );

        if (!File.Exists(outputFilePath))
            throw new Exception(
                $"Failed to convert \"{Path.GetFileName(sourceFile)}\". FFmpeg did not produce the output file.\n\nFFmpeg output:\n{log}"
            );
    }

    private int GetFrameCount(string filePath, CancellationToken cancel)
    {
        int? frameCount = null;

        var (exitCode, log) = Ffmpeg.Run(
            $"-v error -select_streams v:0 -show_entries stream=nb_frames -of default=noprint_wrappers=1:nokey=1 \"{filePath}\"",
            output =>
            {
                if (int.TryParse(output.Trim(), out var frames))
                    frameCount = frames;
            },
            "ffprobe.exe",
            cancel
        );

        if (exitCode != 0)
            throw new Exception(
                $"Failed to inspect \"{Path.GetFileName(filePath)}\". FFprobe failed with exit code {exitCode}.\n\nFFprobe output:\n{log}"
            );

        if (!frameCount.HasValue)
            throw new Exception(
                $"Failed to inspect \"{Path.GetFileName(filePath)}\". FFprobe returned successfully, but did not produce the frame count.\n\nFFprobe output:\n{log}"
            );

        return frameCount.Value;
    }

    private void AddLink_Click(object? sender, EventArgs e)
    {
        using OpenFileDialog f =
            new()
            {
                AutoUpgradeEnabled = true,
                CheckFileExists = true,
                CheckPathExists = true,
                Filter = "All files (*.*)|*.*",
                Multiselect = true,
                RestoreDirectory = true,
                ShowHelp = false,
                ShowHiddenFiles = false,
                Title = "Add Movie Files",
            };

        if (f.ShowDialog(this) == DialogResult.OK)
        {
            foreach (var file in f.FileNames)
            {
                if (!_filesList.Items.Contains(file))
                    _filesList.Items.Add(file);
            }
        }
    }

    private void RemoveLink_Click(object? sender, EventArgs e)
    {
        foreach (int i in _filesList.SelectedIndices.Cast<int>().OrderByDescending(x => x))
            _filesList.Items.RemoveAt(i);
    }

    private void FilesList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        _removeLink.Enabled = _filesList.SelectedIndices.Count > 0;
    }
}
