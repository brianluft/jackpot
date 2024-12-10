using J.Core.Data;

namespace J.App;

public sealed class RecycleBinForm : Form
{
    private readonly LibraryProviderAdapter _libraryProvider;
    private readonly TableLayoutPanel _table;
    private readonly FlowLayoutPanel _topFlow,
        _bottomLeftFlow,
        _bottomRightFlow;
    private readonly Button _emptyButton,
        _restoreButton,
        _deleteButton,
        _closeButton;
    private readonly DataGridView _grid;

    public RecycleBinForm(LibraryProviderAdapter libraryProvider)
    {
        _libraryProvider = libraryProvider;
        Ui ui = new(this);

        Controls.Add(_table = ui.NewTable(2, 3));
        {
            _table.Padding = ui.DefaultPadding;
            _table.RowStyles[1].SizeType = SizeType.Percent;
            _table.RowStyles[1].Height = 100;

            _table.Controls.Add(_topFlow = ui.NewFlowRow(), 0, 0);
            {
                _table.SetColumnSpan(_topFlow, 2);
                _topFlow.Margin += ui.BottomSpacing;

                _topFlow.Controls.Add(_restoreButton = ui.NewButton("Restore"));
                {
                    _restoreButton.Margin += ui.RightSpacing;
                    _restoreButton.Click += RestoreButton_Click;
                }

                _topFlow.Controls.Add(_deleteButton = ui.NewButton("Permanently delete"));
                {
                    _deleteButton.Click += DeleteButton_Click;
                }
            }

            _table.Controls.Add(_grid = ui.NewDataGridView(), 0, 1);
            {
                _table.SetColumnSpan(_grid, 2);
                _grid.Margin += ui.BottomSpacing;
                _grid.ColumnHeadersVisible = false;
                _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                _grid.MultiSelect = true;
                _grid.SelectionChanged += Grid_SelectionChanged;

                var col = _grid.Columns[_grid.Columns.Add("name", "name")];
                {
                    col.DataPropertyName = nameof(Movie.Filename);
                    col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                }
            }

            _table.Controls.Add(_bottomLeftFlow = ui.NewFlowRow(), 0, 2);
            {
                _bottomLeftFlow.Controls.Add(_emptyButton = ui.NewButton("Empty Recycle Bin"));
                {
                    _emptyButton.Click += EmptyButton_Click;
                }
            }

            _table.Controls.Add(_bottomRightFlow = ui.NewFlowRow(), 1, 2);
            {
                _bottomRightFlow.Dock = DockStyle.Right;

                _bottomRightFlow.Controls.Add(_closeButton = ui.NewButton("Close"));
                {
                    _closeButton.Click += CloseButton_Click;
                }
            }
        }

        Text = "Recycle Bin";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = Size = ui.GetSize(500, 500);
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = false;
        AcceptButton = _closeButton;
        CancelButton = _closeButton;
        ShowIcon = false;
        ShowInTaskbar = false;

        UpdateList();
        EnableDisableControls();
    }

    private void UpdateList()
    {
        _grid.DataSource = _libraryProvider.GetMovies().Where(x => x.Deleted).OrderBy(x => x.Filename).ToList();
        EnableDisableControls();
    }

    private void EnableDisableControls()
    {
        _emptyButton.Enabled = _grid.Rows.Count > 0;

        var selected = _grid.SelectedRows.Count > 0;
        _restoreButton.Enabled = selected;
        _deleteButton.Enabled = selected;
    }

    private List<MovieId> GetSelectedMovieIds()
    {
        List<MovieId> movieIds = [];

        foreach (DataGridViewRow row in _grid.SelectedRows)
        {
            var movie = (Movie)row.DataBoundItem!;
            movieIds.Add(movie.Id);
        }

        return movieIds;
    }

    private List<MovieId> GetAllMovieIds()
    {
        List<MovieId> movieIds = [];

        foreach (DataGridViewRow row in _grid.Rows)
        {
            var movie = (Movie)row.DataBoundItem!;
            movieIds.Add(movie.Id);
        }

        return movieIds;
    }

    private void RestoreButton_Click(object? sender, EventArgs e)
    {
        if (_grid.SelectedRows.Count == 0)
            return;

        var movieIds = GetSelectedMovieIds();

        var outcome = ProgressForm.Do(
            this,
            "Restoring movies...",
            async (updateProgress, cancel) =>
            {
                await _libraryProvider.RestoreMoviesAsync(movieIds, updateProgress, cancel).ConfigureAwait(false);
            }
        );

        UpdateList();
    }

    private void DeleteButton_Click(object? sender, EventArgs e)
    {
        if (_grid.SelectedRows.Count == 0)
            return;

        var movieIds = GetSelectedMovieIds();

        PermanentlyDelete(movieIds);
    }

    private void EmptyButton_Click(object? sender, EventArgs e)
    {
        if (_grid.SelectedRows.Count == 0)
            return;

        var movieIds = GetAllMovieIds();

        PermanentlyDelete(movieIds);
    }

    private void PermanentlyDelete(List<MovieId> movieIds)
    {
        string message;
        if (movieIds.Count == 1)
        {
            var movie = _libraryProvider.GetMovie(movieIds[0]);
            message = $"Are you sure you want to permanently delete this movie?\n\n ● {movie.Filename}";
        }
        else
        {
            List<string> names = [];
            foreach (var id in movieIds.Take(5))
            {
                var movie = _libraryProvider.GetMovie(id);
                names.Add(" ● " + movie.Filename);
            }
            if (movieIds.Count > 5)
                names.Add($"(and {movieIds.Count - 5:#,##0} more)");
            message =
                $"Are you sure you want to permanently delete these {movieIds.Count:#,##0} movies?\n\n{string.Join("\n\n", names)}";
        }

        if (
            MessageForm.Show(
                this,
                message,
                "Permanently Delete",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question,
                1
            ) != DialogResult.OK
        )
        {
            return;
        }

        var outcome = ProgressForm.Do(
            this,
            "Permanently deleting movies...",
            async (updateProgress, cancel) =>
            {
                await _libraryProvider
                    .PermanentlyDeleteMoviesAsync(movieIds, updateProgress, cancel)
                    .ConfigureAwait(false);
            }
        );

        UpdateList();
    }

    private void CloseButton_Click(object? sender, EventArgs e)
    {
        Close();
    }

    private void Grid_SelectionChanged(object? sender, EventArgs e)
    {
        EnableDisableControls();
    }
}
