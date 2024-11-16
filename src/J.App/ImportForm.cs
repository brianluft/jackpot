using System.Collections.Frozen;
using System.ComponentModel;

namespace J.App;

public sealed class ImportForm : Form
{
    private readonly LibraryProviderAdapter _libraryProvider;
    private readonly TableLayoutPanel _table;
    private readonly Label _label;
    private readonly ListBox _listBox;
    private readonly FlowLayoutPanel _buttonFlow;
    private readonly Button _startButton,
        _cancelButton;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public List<string> SelectedFilePaths { get; private set; } = [];

    public ImportForm(LibraryProviderAdapter libraryProvider)
    {
        _libraryProvider = libraryProvider;
        Ui ui = new(this);

        Controls.Add(_table = ui.NewTable(1, 3));
        {
            _table.Padding = ui.DefaultPadding;
            _table.RowStyles[1].SizeType = SizeType.Percent;
            _table.RowStyles[1].Height = 100;

            _table.Controls.Add(_label = ui.NewLabel("Drag-and-drop movie files below."), 0, 0);
            {
                _label.Margin = ui.BottomSpacingBig;
            }

            _table.Controls.Add(_listBox = ui.NewListBox(), 0, 1);
            {
                _listBox.AllowDrop = true;
                _listBox.DragEnter += ListBox_DragEnter;
                _listBox.DragDrop += ListBox_DragDrop;
            }

            _table.Controls.Add(_buttonFlow = ui.NewFlowRow(), 0, 2);
            {
                _buttonFlow.Dock = DockStyle.Right;
                _buttonFlow.Margin = ui.TopSpacingBig;
            }

            _buttonFlow.Controls.Add(_startButton = ui.NewButton("Start"));
            {
                _startButton.Click += StartButton_Click;
            }

            _buttonFlow.Controls.Add(_cancelButton = ui.NewButton("Cancel", DialogResult.Cancel));
            {
                _cancelButton.Click += delegate
                {
                    Close();
                };
            }
        }

        Text = "Add to Library";
        StartPosition = FormStartPosition.CenterScreen;
        Size = ui.GetSize(600, 400);
        MinimumSize = ui.GetSize(300, 200);
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = false;
        AcceptButton = _startButton;
        CancelButton = _cancelButton;
        Icon = ui.GetIconResource("App.ico");
        ShowIcon = true;
        ShowInTaskbar = true;
    }

    private void StartButton_Click(object? sender, EventArgs e)
    {
        SelectedFilePaths = _listBox.Items.Cast<string>().ToList();
        DialogResult = DialogResult.OK;
        Close();
    }

    private void ListBox_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data!.GetDataPresent(DataFormats.FileDrop))
            e.Effect = DragDropEffects.Copy;
    }

    private void ListBox_DragDrop(object? sender, DragEventArgs e)
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
                if (!_listBox.Items.Contains(file))
                    _listBox.Items.Add(file);
            }
        }

        RemoveExistingFiles();
    }

    private static readonly FrozenSet<string> _extensions = new[] { ".mp4" }.ToFrozenSet();

    private void AddDirectory(string dir)
    {
        foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
        {
            if (_extensions.Contains(Path.GetExtension(file).ToLowerInvariant()) && !_listBox.Items.Contains(file))
                _listBox.Items.Add(file);
        }
    }

    private void RemoveExistingFiles()
    {
        var filenames = _libraryProvider.GetMovies().Select(x => x.Filename).ToHashSet();
        for (int i = _listBox.Items.Count - 1; i >= 0; i--)
        {
            if (filenames.Contains(Path.GetFileNameWithoutExtension((string)_listBox.Items[i])))
                _listBox.Items.RemoveAt(i);
        }
    }
}
