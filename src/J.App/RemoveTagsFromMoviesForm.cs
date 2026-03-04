using J.Core.Data;

namespace J.App;

public sealed class RemoveTagsFromMoviesForm : Form
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

    public RemoveTagsFromMoviesForm(LibraryProviderAdapter libraryProvider)
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

                _data = [];
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

        Text = "Remove Tags";
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
    }

    public void Initialize(IEnumerable<MovieId> movieIds)
    {
        _movieIds.AddRange(movieIds);

        var tagTypes = _libraryProvider.GetTagTypes().ToDictionary(x => x.Id);
        var tags = _libraryProvider.GetTags().ToDictionary(x => x.Id);

        HashSet<TagId> movieTagIds = [];
        foreach (var movieId in _movieIds)
        foreach (var mt in _libraryProvider.GetMovieTags(movieId))
            movieTagIds.Add(mt.TagId);

        _data.AddRange(
            from tagId in movieTagIds
            let tag = tags[tagId]
            let tagType = tagTypes[tag.TagTypeId]
            orderby tagType.SortIndex, tagType.SingularName, tag.Name
            select new Row(tag, tagType, $"{tagType.SingularName}  🞂  {tag.Name}")
        );

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

    private void OkButton_Click(object? sender, EventArgs e)
    {
        if (_grid.SelectedRows.Count == 0)
        {
            MessageForm.Show(this, "Please select a tag.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Ok();
    }

    private void Ok()
    {
        List<MovieTag> movieTags = [];
        foreach (DataGridViewRow row in _grid.SelectedRows)
        {
            var tagId = ((Row)row.DataBoundItem!).Tag.Id;
            foreach (var movieId in _movieIds)
                movieTags.Add(new MovieTag(movieId, tagId));
        }

        var outcome = ProgressForm.Do(
            this,
            "Removing tags...",
            async (updateProgress, cancel) =>
            {
                await _libraryProvider.DeleteMovieTagsAsync(movieTags, updateProgress, cancel).ConfigureAwait(false);
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
