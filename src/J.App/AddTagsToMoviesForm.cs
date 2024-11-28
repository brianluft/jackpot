using J.Core.Data;

namespace J.App;

public sealed class AddTagsToMoviesForm : Form
{
    private readonly LibraryProviderAdapter _libraryProvider;
    private readonly TableLayoutPanel _table;
    private readonly MyTextBox _searchText;
    private readonly DataGridView _grid;
    private readonly FlowLayoutPanel _buttonFlow;
    private readonly Button _okButton,
        _cancelButton;
    private readonly List<MovieId> _movieIds = [];
    private readonly List<Row> _data;
    private readonly System.Windows.Forms.Timer _searchTimer;

    public AddTagsToMoviesForm(LibraryProviderAdapter libraryProvider)
    {
        _libraryProvider = libraryProvider;
        Ui ui = new(this);

        _searchTimer = new() { Enabled = false, Interval = 250 };
        {
            Disposed += delegate
            {
                _searchTimer.Dispose();
            };
            _searchTimer.Tick += SearchTimer_Tick;
        }

        Controls.Add(_table = ui.NewTable(1, 3));
        {
            _table.Padding = ui.DefaultPadding;
            _table.RowStyles[1].SizeType = SizeType.Percent;
            _table.RowStyles[1].Height = 100;

            _table.Controls.Add(ui.NewLabeledPair("Search:", _searchText = ui.NewTextBox(300)), 1, 0);
            {
                _searchText.Margin = ui.BottomSpacing;
                _searchText.SetCueText("Search");
                _searchText.TextChanged += SearchText_TextChanged;
            }

            _table.Controls.Add(_grid = ui.NewDataGridView(), 0, 1);
            {
                _grid.ColumnHeadersVisible = false;
                _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                _grid.MultiSelect = true;

                var col = _grid.Columns[_grid.Columns.Add(null, null)];
                {
                    col.DataPropertyName = nameof(Row.Display);
                    col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                }

                _grid.CellDoubleClick += Grid_CellDoubleClick;
                var tagTypes = _libraryProvider.GetTagTypes().ToDictionary(x => x.Id);

                _data = (
                    from tag in _libraryProvider.GetTags()
                    let tagType = tagTypes[tag.TagTypeId]
                    orderby tagType.SortIndex, tagType.SingularName, tag.Name
                    select new Row(tag, tagType, $"{tagType.SingularName}  🞂  {tag.Name}")
                ).ToList();
            }

            _table.Controls.Add(_buttonFlow = ui.NewFlowRow(), 0, 2);
            {
                _buttonFlow.Dock = DockStyle.Right;
                _buttonFlow.Margin = ui.TopSpacingBig;

                _buttonFlow.Controls.Add(_okButton = ui.NewButton("OK"));
                {
                    _okButton.Click += OkButton_Click;
                    _okButton.Margin += ui.ButtonSpacing;
                }

                _buttonFlow.Controls.Add(_cancelButton = ui.NewButton("Cancel", DialogResult.Cancel));
            }
        }

        Text = "Add Tags";
        StartPosition = FormStartPosition.CenterParent;
        Size = ui.GetSize(500, 500);
        MinimumSize = ui.GetSize(300, 300);
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = false;
        AcceptButton = _okButton;
        CancelButton = _cancelButton;
        ShowIcon = false;
        ShowInTaskbar = false;

        UpdateList();
    }

    private void SearchTimer_Tick(object? sender, EventArgs e)
    {
        _searchTimer.Stop();
        UpdateList();
    }

    private void SearchText_TextChanged(object? sender, EventArgs e)
    {
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void UpdateList()
    {
        var words = _searchText.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        _grid.DataSource = _data
            .Where(x => words.All(word => x.Display.Contains(word, StringComparison.CurrentCultureIgnoreCase)))
            .ToList();
    }

    private void Grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _grid.RowCount)
            return;

        Ok();
    }

    public void Initialize(IEnumerable<MovieId> movieIds)
    {
        _movieIds.AddRange(movieIds);
    }

    private void OkButton_Click(object? sender, EventArgs e)
    {
        if (_grid.SelectedRows.Count == 0)
        {
            MessageBox.Show("Please select a tag.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Ok();
    }

    private void Ok()
    {
        List<(MovieId MovieId, TagId TagId)> movieTags = [];
        foreach (DataGridViewRow row in _grid.SelectedRows)
        {
            var tagId = ((Row)row.DataBoundItem!).Tag.Id;
            foreach (var movieId in _movieIds)
                movieTags.Add((movieId, tagId));
        }

        var outcome = ProgressForm.Do(
            this,
            "Adding tags...",
            async (updateProgress, cancel) =>
            {
                await _libraryProvider.AddMovieTagsAsync(movieTags, updateProgress, cancel).ConfigureAwait(false);
            }
        );

        if (outcome == Outcome.Success)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private readonly record struct Row(Tag Tag, TagType TagType, string Display);
}
