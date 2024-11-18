using System.Collections.Frozen;
using System.Data;
using System.Text.RegularExpressions;
using J.Core.Data;
using Microsoft.Extensions.DependencyInjection;

namespace J.App;

public sealed partial class EditMoviesForm : Form
{
    private readonly ContextMenuStrip _contextMenuStrip;
    private readonly LibraryProviderAdapter _libraryProvider;
    private readonly IServiceProvider _serviceProvider;
    private readonly MovieExporter _movieExporter;
    private readonly DataGridView _grid;
    private readonly DataTable _data;
    private readonly TableLayoutPanel _table;
    private readonly FlowLayoutPanel _buttonFlow;
    private readonly Button _closeButton;
    private readonly MyTextBox _searchText;

    public EditMoviesForm(
        LibraryProviderAdapter libraryProvider,
        IServiceProvider serviceProvider,
        MovieExporter movieExporter
    )
    {
        _libraryProvider = libraryProvider;
        _serviceProvider = serviceProvider;
        _movieExporter = movieExporter;
        Ui ui = new(this);

        var tagTypes = _libraryProvider.GetTagTypes().OrderBy(x => x.SortIndex).ToList();

        _data = new();
        _data.Columns.Add("id", typeof(MovieId));
        _data.Columns.Add("name", typeof(string));
        foreach (var tagType in tagTypes)
            _data.Columns.Add(tagType.Id.Value, typeof(string));
        _data.Columns.Add("date_added", typeof(DateTimeOffset));

        _contextMenuStrip = ui.NewContextMenuStrip();
        {
            ToolStripMenuItem item;

            foreach (var tagType in tagTypes)
            {
                item = ui.NewToolStripMenuItem($"Add {tagType.SingularName.ToLower()}...");
                item.Click += delegate
                {
                    AddTag(tagType);
                };
                _contextMenuStrip.Items.Add(item);
            }

            item = ui.NewToolStripMenuItem("Remove tag...");
            item.Click += delegate
            {
                RemoveTag();
            };
            _contextMenuStrip.Items.Add(item);

            _contextMenuStrip.Items.Add(ui.NewToolStripSeparator());

            item = ui.NewToolStripMenuItem("Export to MP4...");
            item.Click += ExportMovie_Click;
            _contextMenuStrip.Items.Add(item);

            _contextMenuStrip.Items.Add(ui.NewToolStripSeparator());

            item = ui.NewToolStripMenuItem("Rename...");
            item.Click += RenameMovie_Click;
            _contextMenuStrip.Items.Add(item);

            item = ui.NewToolStripMenuItem("Delete");
            item.Click += DeleteMovie_Click;
            _contextMenuStrip.Items.Add(item);
        }

        Controls.Add(_table = ui.NewTable(1, 2));
        {
            _table.Controls.Add(_buttonFlow = ui.NewFlowRow());
            {
                _buttonFlow.Padding = ui.DefaultPadding;

                _buttonFlow.Controls.Add(_closeButton = ui.NewButton("Close"));
                {
                    _closeButton.Margin += ui.RightSpacing;
                    _closeButton.Click += CloseButton_Click;
                }

                _buttonFlow.Controls.Add(_searchText = ui.NewTextBox(200));
                {
                    _searchText.Margin += ui.GetPadding(0, 4);
                    _searchText.KeyPress += SearchText_KeyPress;
                    _searchText.SetCueText("Search");
                }
            }

            _table.Controls.Add(_grid = ui.NewDataGridView(), 0, 1);
            {
                _grid.RowContextMenuStripNeeded += Grid_RowContextMenuStripNeeded;
                _grid.DataSource = _data;

                var col_name = _grid.Columns[_grid.Columns.Add("name", "Name")];
                {
                    col_name.Width = ui.GetLength(550);
                    col_name.DataPropertyName = "name";
                    col_name.Frozen = true;
                }

                var col_date_added = _grid.Columns[_grid.Columns.Add("date_added", "Date Added")];
                {
                    col_date_added.Width = ui.GetLength(225);
                    col_date_added.DataPropertyName = "date_added";
                    col_date_added.DividerWidth = ui.GetLength(3);
                    col_date_added.Frozen = true;
                }

                foreach (var tagType in tagTypes)
                {
                    var col = _grid.Columns[_grid.Columns.Add(tagType.Id.Value, tagType.PluralName)];
                    {
                        col.Width = ui.GetLength(200);
                        col.DataPropertyName = tagType.Id.Value;
                    }
                }
            }
        }

        Text = "Edit Movies";
        StartPosition = FormStartPosition.CenterScreen;
        Size = ui.GetSize(1200, 600);
        MinimumSize = ui.GetSize(400, 300);
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = true;
        Icon = ui.GetIconResource("App.ico");
        ShowIcon = true;
        ShowInTaskbar = false;
        WindowState = FormWindowState.Maximized;
    }

    private void Grid_RowContextMenuStripNeeded(object? sender, DataGridViewRowContextMenuStripNeededEventArgs e)
    {
        e.ContextMenuStrip = _grid.SelectedRows.Count > 0 ? _contextMenuStrip : null;
    }

    private void SearchText_KeyPress(object? sender, KeyPressEventArgs e)
    {
        // Did they press the enter key with no modifiers?
        if (e.KeyChar == (char)Keys.Enter && ModifierKeys == Keys.None)
        {
            e.Handled = true;
            UpdateList();
        }
    }

    private void CloseButton_Click(object? sender, EventArgs e)
    {
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _data.Dispose();
        }
    }

    private void AddTag(TagType type)
    {
        using var f = _serviceProvider.GetRequiredService<EditMoviesChooseTagForm>();
        f.Initialize(type);
        f.Text = $"Add {type.SingularName}";
        if (f.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var tagId = f.SelectedTag!;

            List<(MovieId MovieId, TagId TagId)> list = [];
            foreach (DataGridViewRow viewRow in _grid.SelectedRows)
            {
                var row = ((DataRowView)viewRow.DataBoundItem!).Row;
                var movieId = (MovieId)row["id"];
                list.Add((movieId, tagId));
            }

            SimpleProgressForm.Do(
                this,
                "Tagging movies...",
                async (updateProgress, cancel) =>
                {
                    await _libraryProvider.AddMovieTagsAsync(list, updateProgress, cancel).ConfigureAwait(true);
                }
            );

            UpdateList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RemoveTag()
    {
        List<MovieId> movieIds = [];
        HashSet<TagId> tagIds = [];
        foreach (DataGridViewRow viewRow in _grid.SelectedRows)
        {
            var row = ((DataRowView)viewRow.DataBoundItem!).Row;
            var movieId = (MovieId)row["id"];
            movieIds.Add(movieId);
            var movieTags = _libraryProvider.GetMovieTags(movieId);
            foreach (var x in movieTags)
                tagIds.Add(x.TagId);
        }

        var tags = from tagId in tagIds join tag in _libraryProvider.GetTags() on tagId equals tag.Id select tag;

        using var f = _serviceProvider.GetRequiredService<EditMoviesRemoveTagForm>();
        f.Initialize(tags);
        f.Text = $"Remove Tags";
        if (f.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var removeTagIds = f.SelectedTags.ToHashSet();
            List<MovieTag> removeMovieTags = [];

            foreach (var movieId in movieIds)
            {
                var movieTags = _libraryProvider.GetMovieTags(movieId).Where(x => removeTagIds.Contains(x.TagId));
                removeMovieTags.AddRange(movieTags);
            }

            SimpleProgressForm.Do(
                this,
                "Untagging movies...",
                async (updateProgress, cancel) =>
                {
                    await _libraryProvider
                        .DeleteMovieTagsAsync(removeMovieTags, updateProgress, cancel)
                        .ConfigureAwait(true);
                }
            );

            UpdateList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RenameMovie_Click(object? sender, EventArgs e)
    {
        var count = _grid.SelectedRows.Count;

        if (count == 0)
            return;

        if (count > 1)
        {
            MessageBox.Show(this, "Please select a single movie.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var row = ((DataRowView)_grid.SelectedRows[0].DataBoundItem!).Row;
        var id = (MovieId)row["id"];
        var oldName = (string)row["name"];

        using EditMoviesRenameMovieForm f = new(oldName);
        if (f.ShowDialog(this) != DialogResult.OK)
            return;

        var newName = f.NewName;
        var movie = _libraryProvider.GetMovie(id) with { Filename = newName };

        SimpleProgressForm.Do(
            this,
            "Renaming...",
            async (updateProgress, cancel) =>
            {
                await _libraryProvider.UpdateMovieAsync(movie, updateProgress, cancel).ConfigureAwait(false);
            }
        );

        UpdateList();
    }

    private void DeleteMovie_Click(object? sender, EventArgs e)
    {
        var count = _grid.SelectedRows.Count;
        if (count == 0)
            return;

        if (
            MessageBox.Show($"Delete {count} movie(s)?", "Confirm", MessageBoxButtons.OKCancel, MessageBoxIcon.Question)
            != DialogResult.OK
        )
        {
            return;
        }

        try
        {
            List<MovieId> movieIds = [];
            foreach (DataGridViewRow viewRow in _grid.SelectedRows)
            {
                var row = ((DataRowView)viewRow.DataBoundItem!).Row;
                var id = (MovieId)row["id"];
                movieIds.Add(id);
            }

            using SimpleProgressForm f =
                new(
                    (updateProgress, updateMessage, cancel) =>
                    {
                        updateMessage("Deleting...");

                        var count = movieIds.Count;
                        var i = 0;
                        foreach (var id in movieIds)
                        {
                            _libraryProvider.DeleteMovieAsync(id, updateProgress, cancel).GetAwaiter().GetResult();

                            i++;
                            updateProgress((double)i / count);
                        }
                    }
                );

            var result = f.ShowDialog(this);
            if (result == DialogResult.Abort)
            {
                MessageBox.Show(
                    this,
                    f.Exception!.SourceException.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }

            UpdateList();
        }
        catch (Exception ex)
        {
            Enabled = true;
            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Enabled = true;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        UpdateList();
    }

    private void UpdateList()
    {
        var selectedIds = _grid
            .SelectedRows.Cast<DataGridViewRow>()
            .Select(x => (MovieId)((DataRowView)x.DataBoundItem!).Row["id"])
            .ToFrozenSet();
        var firstRowIndex = _grid.FirstDisplayedScrollingRowIndex;
        var firstColumnIndex = _grid.FirstDisplayedScrollingColumnIndex;

        var movies = _libraryProvider.GetMovies();
        var tagTypes = _libraryProvider.GetTagTypes().ToDictionary(x => x.Id);
        var tags = _libraryProvider.GetTags().ToDictionary(x => x.Id);
        var movieTags = _libraryProvider.GetMovieTags().ToLookup(x => x.MovieId, x => x.TagId);

        var searchTerms = WhitespaceRegex().Split(_searchText.Text);
        movies = movies
            .Where(movie =>
            {
                var thisMovieTags = movieTags[movie.Id].Select(tagId => tags[tagId]).ToList();

                foreach (var term in searchTerms)
                {
                    if (movie.Filename.Contains(term, StringComparison.InvariantCultureIgnoreCase))
                        continue;

                    var match = false;
                    foreach (var tag in thisMovieTags)
                    {
                        if (tag.Name.Contains(term, StringComparison.InvariantCultureIgnoreCase))
                        {
                            match = true;
                            break;
                        }
                    }
                    if (match)
                        continue;

                    return false;
                }

                return true;
            })
            .ToList();

        _grid.DataSource = null;
        _data.Rows.Clear();

        foreach (var movie in movies.OrderByDescending(x => x.DateAdded).ThenBy(x => x.Filename))
        {
            var row = _data.NewRow();
            row["id"] = movie.Id;
            row["name"] = movie.Filename;
            row["date_added"] = movie.DateAdded;

            Dictionary<TagTypeId, List<string>> rowTags = [];
            foreach (var tagType in tagTypes)
                rowTags[tagType.Key] = [];

            foreach (var tagId in movieTags[movie.Id])
            {
                var tag = tags[tagId];
                rowTags[tag.TagTypeId].Add(tag.Name);
            }

            foreach (var pair in rowTags)
            {
                pair.Value.Sort();
                row[pair.Key.Value] = string.Join(", ", pair.Value);
            }

            _data.Rows.Add(row);
        }

        _grid.DataSource = _data;

        // Restore scroll position, if possible (it might be beyond the new bounds).
        if (firstRowIndex >= 0 && firstRowIndex < _grid.RowCount)
            _grid.FirstDisplayedScrollingRowIndex = firstRowIndex;
        if (firstColumnIndex >= 0 && firstColumnIndex < _grid.ColumnCount)
            _grid.FirstDisplayedScrollingColumnIndex = firstColumnIndex;

        // Re-select the originally selected rows, if possible (they might have been deleted).
        _grid.ClearSelection();
        foreach (DataGridViewRow viewRow in _grid.Rows)
        {
            var row = ((DataRowView)viewRow.DataBoundItem!).Row;
            var id = (MovieId)row["id"];
            if (selectedIds.Contains(id))
                viewRow.Selected = true;
        }
    }

    private void ExportMovie_Click(object? sender, EventArgs e)
    {
        // Prompt for output directory.
        using FolderBrowserDialog b =
            new()
            {
                AutoUpgradeEnabled = true,
                Description = "Select Output Directory",
                ShowNewFolderButton = true,
                RootFolder = Environment.SpecialFolder.Desktop,
                UseDescriptionForTitle = true,
            };
        if (b.ShowDialog(this) != DialogResult.OK)
            return;
        var outDir = b.SelectedPath;

        var movies = _libraryProvider.GetMovies().ToDictionary(x => x.Id);

        using SimpleProgressForm f =
            new(
                (updateProgress, updateMessage, cancel) =>
                {
                    var count = _grid.SelectedRows.Count;
                    var i = 0;
                    foreach (DataGridViewRow viewRow in _grid.SelectedRows)
                    {
                        var row = ((DataRowView)viewRow.DataBoundItem!).Row;
                        var movieId = (MovieId)row["id"];
                        var movie = movies[movieId];
                        var outFilePath = Path.Combine(outDir, movie.Filename);

                        // Ensure .mp4 extension.
                        if (!Path.GetExtension(outFilePath).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
                            outFilePath += ".mp4";

                        var name = (string)row["name"];
                        if (name.Length > 75)
                            name = name[..75] + "...";

                        updateMessage($"File {i + 1:#,##0} of {count:#,##0}\n{name}");

                        if (!File.Exists(outFilePath))
                            _movieExporter.Export(movie, outFilePath, updateProgress, cancel);

                        i++;
                        updateProgress((double)i / count);
                    }
                }
            );

        if (f.ShowDialog(this) == DialogResult.Abort)
        {
            MessageBox.Show(
                this,
                f.Exception!.SourceException.Message,
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
