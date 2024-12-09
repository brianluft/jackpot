using System.Data;
using System.Web;
using J.Core.Data;
using Microsoft.Web.WebView2.WinForms;

namespace J.App;

public sealed class MoviePropertiesForm : Form
{
    private readonly LibraryProviderAdapter _libraryProvider;
    private readonly Client _client;
    private readonly TableLayoutPanel _table;
    private readonly FlowLayoutPanel _addRemoveTagFlow,
        _okCancelFlow;
    private readonly WebView2 _webView;
    private readonly MyTextBox _nameText;
    private readonly ComboBox _addTagCombo;
    private readonly DataGridView _tagsGrid;
    private readonly Button _addTagButton,
        _removeTagButton,
        _okButton,
        _cancelButton;
    private readonly DataTable _tagsTable;
    private Movie? _movie;

    public MoviePropertiesForm(LibraryProviderAdapter libraryProvider, Client client)
    {
        _libraryProvider = libraryProvider;
        _client = client;
        Ui ui = new(this);

        _tagsTable = new();
        _tagsTable.Columns.Add("tag_id", typeof(TagId));
        _tagsTable.Columns.Add("display", typeof(string));

        Controls.Add(_table = ui.NewTable(1, 5));
        {
            _table.RowStyles[3].SizeType = SizeType.Percent;
            _table.RowStyles[3].Height = 100;

            _table.Controls.Add(_webView = ui.NewWebView2(), 0, 0);
            {
                _webView.Size = ui.GetSize(444, 250);
                _webView.Margin += ui.BottomSpacing;
            }

            _table.Controls.Add(ui.NewLabeledPair("&Name:", _nameText = ui.NewWordWrapTextbox(450, 50)), 0, 1);
            {
                _nameText.Margin += ui.BottomSpacing;
            }

            _addRemoveTagFlow = ui.NewFlowRow();
            {
                _addRemoveTagFlow.Controls.Add(_addTagCombo = ui.NewAutoCompleteDropDown(300));
                {
                    _addTagCombo.PreviewKeyDown += AddTagCombo_PreviewKeyDown;
                    _addTagCombo.KeyDown += AddTagCombo_KeyDown;
                    _addTagCombo.DisplayMember = "Name";
                    _addTagCombo.ValueMember = "Value";
                }

                _addRemoveTagFlow.Controls.Add(_addTagButton = ui.NewButton("➕"));
                {
                    _addTagButton.AutoSize = false;
                    _addTagButton.Width = ui.GetLength(60);
                    _addTagButton.Padding = Padding.Empty;
                    _addTagButton.Click += AddTagButton_Click;
                }

                _addRemoveTagFlow.Controls.Add(_removeTagButton = ui.NewButton("➖"));
                {
                    _removeTagButton.AutoSize = false;
                    _removeTagButton.Width = ui.GetLength(60);
                    _removeTagButton.Padding = Padding.Empty;
                    _removeTagButton.Enabled = false;
                    _removeTagButton.Click += RemoveTagButton_Click;
                }

                _addTagCombo.SizeChanged += delegate
                {
                    _addTagButton.Height = _removeTagButton.Height =
                        _addTagCombo.Height + 2 * ui.BuiltInVisualButtonPadding;
                    _addTagButton.Margin =
                        new Padding(0, _addTagCombo.Margin.Top - ui.BuiltInVisualButtonPadding, 0, 0) + ui.LeftSpacing;
                    _removeTagButton.Margin = _addTagButton.Margin;
                };
            }

            _table.Controls.Add(ui.NewLabeledPair("&Tags:", _addRemoveTagFlow), 0, 2);

            _table.Controls.Add(_tagsGrid = ui.NewDataGridView(), 0, 3);
            {
                _tagsGrid.Margin += ui.BottomSpacing;
                _tagsGrid.ColumnHeadersVisible = false;

                var col = _tagsGrid.Columns[_tagsGrid.Columns.Add("display", "Display")];
                {
                    col.DataPropertyName = "display";
                    col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                }

                _tagsGrid.SelectionChanged += TagsGrid_SelectionChanged;
            }

            _table.Controls.Add(_okCancelFlow = ui.NewFlowRow(), 0, 4);
            {
                _okCancelFlow.Dock = DockStyle.Right;

                _okCancelFlow.Controls.Add(_okButton = ui.NewButton("OK"));
                {
                    _okButton.Margin += ui.ButtonSpacing;
                    _okButton.Click += OkButton_Click;
                }

                _okCancelFlow.Controls.Add(_cancelButton = ui.NewButton("Cancel", DialogResult.Cancel));
            }
        }

        Text = "Movie Properties";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = Size = ui.GetSize(500, 700);
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        Padding = ui.DefaultPadding;
        AcceptButton = _okButton;
        CancelButton = _cancelButton;
    }

    public void Initialize(MovieId movieId)
    {
        var movie = _libraryProvider.GetMovie(movieId);
        _movie = movie;
        var allTagsDict = _libraryProvider.GetTags().ToDictionary(x => x.Id);
        var allTagTypesDict = _libraryProvider.GetTagTypes().ToDictionary(x => x.Id);
        var tags = _libraryProvider.GetMovieTags(movieId).Select(x => allTagsDict[x.TagId]);

        // Populate the name textbox
        _nameText.Text = movie.Filename;
        _nameText.Select(0, 0);

        // Populate the tags grid
        foreach (
            var (tagTypeName, tag) in from tag in tags
            let tagType = allTagTypesDict[tag.TagTypeId]
            orderby tagType.SortIndex, tagType.SingularName, tag.Name
            select (tagType.SingularName, tag)
        )
        {
            var row = _tagsTable.NewRow();
            row["tag_id"] = tag.Id;
            row["display"] = $"{tag.Name} ({tagTypeName})";
            _tagsTable.Rows.Add(row);
        }

        _tagsGrid.DataSource = _tagsTable;

        // Populate the tags combo
        foreach (
            var item in from tag in allTagsDict.Values
            let tagType = allTagTypesDict[tag.TagTypeId]
            orderby tagType.SortIndex, tagType.SingularName, tag.Name
            select new TagsComboRow($"{tag.Name} ({tagType.SingularName})", tag.Id)
        )
        {
            _addTagCombo.Items.Add(item);
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        var query = HttpUtility.ParseQueryString("");
        query["movieId"] = _movie!.Value.Id!.Value;
        query["sessionPassword"] = _client.SessionPassword;
        _webView.Source = new($"http://localhost:{_client.Port}/movie-preview.html?{query}");

        _tagsGrid.ClearSelection();

        _nameText.Focus();
    }

    private void AddTagCombo_PreviewKeyDown(object? sender, PreviewKeyDownEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
            e.IsInputKey = true;
    }

    private void AddTagCombo_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && e.Modifiers == Keys.None)
        {
            AddTag();
            e.Handled = true;
        }
    }

    private void AddTagButton_Click(object? sender, EventArgs e)
    {
        AddTag();
    }

    private void AddTag()
    {
        // Has the user selected one of the defined tags?
        if (_addTagCombo.SelectedItem is TagsComboRow selectedTag)
        {
            // Is the tag already in the list?
            if (!TagExists(selectedTag.Value))
            {
                var tag = _libraryProvider.GetTag(selectedTag.Value);
                var tagType = _libraryProvider.GetTagType(tag.TagTypeId);
                var row = _tagsTable.NewRow();
                row["tag_id"] = tag.Id;
                row["display"] = $"{tag.Name} ({tagType.SingularName})";
                _tagsTable.Rows.Add(row);

                _tagsGrid.ClearSelection();
                _tagsGrid.Rows[^1].Selected = true;
            }

            _addTagCombo.Text = "";
        }
        else
        {
            MessageBox.Show(
                this,
                "Please select a tag from the list.",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private void TagsGrid_SelectionChanged(object? sender, EventArgs e)
    {
        _removeTagButton.Enabled = _tagsGrid.SelectedRows.Count > 0;
    }

    private void RemoveTagButton_Click(object? sender, EventArgs e)
    {
        HashSet<TagId> tagIds = new(_tagsGrid.SelectedRows.Count);

        foreach (DataGridViewRow dataGridViewRow in _tagsGrid.SelectedRows)
        {
            var dataRowView = (DataRowView)dataGridViewRow.DataBoundItem!;
            var dataRow = dataRowView.Row;
            tagIds.Add((TagId)dataRow["tag_id"]);
        }

        for (var i = _tagsTable.Rows.Count - 1; i >= 0; i--)
        {
            var tagId = (TagId)_tagsTable.Rows[i]["tag_id"];

            if (tagIds.Contains(tagId))
                _tagsTable.Rows.RemoveAt(i);
        }
    }

    private bool TagExists(TagId tagId)
    {
        foreach (DataRow row in _tagsTable.Rows)
        {
            if ((TagId)row["tag_id"] == tagId)
                return true;
        }

        return false;
    }

    private void OkButton_Click(object? sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_nameText.Text))
                throw new Exception("Please enter a name.");

            var movie = _movie!.Value with { Filename = _nameText.Text };

            List<TagId> tagIds = new(_tagsTable.Rows.Count);
            foreach (DataRow row in _tagsTable.Rows)
                tagIds.Add((TagId)row["tag_id"]);

            var outcome = ProgressForm.Do(
                this,
                "Saving changes...",
                async (updateProgress, cancel) =>
                {
                    await _libraryProvider
                        .UpdateMovieAsync(movie, tagIds, updateProgress, cancel)
                        .ConfigureAwait(false);
                }
            );

            if (outcome == Outcome.Success)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private readonly record struct TagsComboRow(string Name, TagId Value);
}
