using System.Data;
using System.Diagnostics;
using J.Core.Data;
using Microsoft.Extensions.DependencyInjection;

namespace J.App;

public sealed class TagsControl : UserControl
{
    private readonly LibraryProviderAdapter _libraryProvider;
    private readonly IServiceProvider _serviceProvider;
    private readonly TableLayoutPanel _table,
        _leftTable,
        _rightTable;
    private readonly FlowLayoutPanel _leftButtonFlow,
        _rightButtonFlow;
    private readonly Button _leftNewGroupButton,
        _leftRenameGroupButton,
        _leftMoveGroupUpButton,
        _leftMoveGroupDownButton,
        _leftDeleteGroupButton,
        _rightNewTagButton,
        _rightRenameTagButton,
        _rightDeleteTagButton;
    private readonly DataGridView _leftGrid,
        _rightGrid;
    private readonly DataTable _leftData,
        _rightData;
    private Dictionary<TagTypeId, TagType> _tagTypes = [];

    public event EventHandler? TagTypeChanged;
    public event EventHandler? TagChanged;

    public TagsControl(LibraryProviderAdapter libraryProvider, IServiceProvider serviceProvider)
    {
        _libraryProvider = libraryProvider;
        _serviceProvider = serviceProvider;
        Ui ui = new(this);

        _leftData = new();
        {
            _leftData.Columns.Add("id", typeof(TagTypeId));
            _leftData.Columns.Add("name", typeof(string));
        }

        _rightData = new();
        {
            _rightData.Columns.Add("id", typeof(TagId));
            _rightData.Columns.Add("name", typeof(string));
        }

        Controls.Add(_table = ui.NewTable(2, 1));
        {
            _table.Padding = ui.DefaultPadding;
            _table.ColumnStyles[0].SizeType = SizeType.Absolute;
            _table.ColumnStyles[0].Width = ui.GetLength(500);
            _table.ColumnStyles[1].SizeType = SizeType.Percent;
            _table.ColumnStyles[1].Width = 100;

            _table.Controls.Add(_leftTable = ui.NewTable(2, 1), 0, 0);
            {
                _leftTable.Margin += ui.GetPadding(0, 0, 64, 0);
                _leftTable.ColumnStyles[1].SizeType = SizeType.Percent;
                _leftTable.ColumnStyles[1].Width = 100;

                _leftTable.Controls.Add(_leftButtonFlow = ui.NewFlowColumn(), 0, 0);
                {
                    _leftButtonFlow.Controls.Add(_leftNewGroupButton = ui.NewButton("New group..."));
                    {
                        _leftNewGroupButton.Dock = DockStyle.Fill;
                        _leftNewGroupButton.Margin += ui.BottomSpacing;
                        _leftNewGroupButton.Click += LeftNewGroupButton_Click;
                    }

                    _leftButtonFlow.Controls.Add(_leftRenameGroupButton = ui.NewButton("Rename..."));
                    {
                        _leftRenameGroupButton.Dock = DockStyle.Fill;
                        _leftRenameGroupButton.Click += LeftRenameGroupButton_Click;
                    }

                    _leftButtonFlow.Controls.Add(_leftMoveGroupUpButton = ui.NewButton("Move up"));
                    {
                        _leftMoveGroupUpButton.Dock = DockStyle.Fill;
                        _leftMoveGroupUpButton.Click += LeftMoveGroupUpButton_Click;
                    }

                    _leftButtonFlow.Controls.Add(_leftMoveGroupDownButton = ui.NewButton("Move down"));
                    {
                        _leftMoveGroupDownButton.Dock = DockStyle.Fill;
                        _leftMoveGroupDownButton.Margin += ui.BottomSpacing;
                        _leftMoveGroupDownButton.Click += LeftMoveGroupDownButton_Click;
                    }

                    _leftButtonFlow.Controls.Add(_leftDeleteGroupButton = ui.NewButton("Delete"));
                    {
                        _leftDeleteGroupButton.Dock = DockStyle.Fill;
                        _leftDeleteGroupButton.Click += LeftDeleteGroupButton_Click;
                    }
                }

                _leftTable.Controls.Add(_leftGrid = ui.NewDataGridView(), 1, 0);
                {
                    _leftGrid.Margin += ui.LeftSpacing;
                    _leftGrid.SelectionChanged += LeftGrid_SelectionChanged;
                    _leftGrid.CellDoubleClick += LeftGrid_CellDoubleClick;
                    _leftGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                    _leftGrid.ColumnHeadersVisible = false;
                    _leftGrid.DefaultCellStyle.Padding += ui.GetPadding(5, 0, 0, 0);

                    var col = _leftGrid.Columns[_leftGrid.Columns.Add("name", "Group")];
                    {
                        col.DataPropertyName = "name";
                        col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    }

                    _leftGrid.DataSource = _leftData;
                }
            }

            _table.Controls.Add(_rightTable = ui.NewTable(2, 1), 1, 0);
            {
                _rightTable.ColumnStyles[1].SizeType = SizeType.Percent;
                _rightTable.ColumnStyles[1].Width = 100;

                _rightTable.Controls.Add(_rightButtonFlow = ui.NewFlowColumn(), 0, 0);
                {
                    _rightButtonFlow.Controls.Add(_rightNewTagButton = ui.NewButton("New tag..."));
                    {
                        _rightNewTagButton.Dock = DockStyle.Fill;
                        _rightNewTagButton.Margin += ui.BottomSpacing;
                        _rightNewTagButton.Click += RightNewTagButton_Click;
                    }

                    _rightButtonFlow.Controls.Add(_rightRenameTagButton = ui.NewButton("Rename..."));
                    {
                        _rightRenameTagButton.Dock = DockStyle.Fill;
                        _rightRenameTagButton.Margin += ui.BottomSpacing;
                        _rightRenameTagButton.Click += RightRenameTagButton_Click;
                    }

                    _rightButtonFlow.Controls.Add(_rightDeleteTagButton = ui.NewButton("Delete"));
                    {
                        _rightDeleteTagButton.Dock = DockStyle.Fill;
                        _rightDeleteTagButton.Click += RightDeleteTagButton_Click;
                    }
                }

                _rightTable.Controls.Add(_rightGrid = ui.NewDataGridView(), 1, 0);
                {
                    _rightGrid.Margin += ui.LeftSpacing;
                    _rightGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                    _rightGrid.MultiSelect = true;
                    _rightGrid.SelectionChanged += RightGrid_SelectionChanged;
                    _rightGrid.CellDoubleClick += RightGrid_CellDoubleClick;
                    _rightGrid.ColumnHeadersVisible = false;
                    _rightGrid.DefaultCellStyle.Padding += ui.GetPadding(5, 0, 0, 0);

                    var col = _rightGrid.Columns[_rightGrid.Columns.Add("name", "Tag")];
                    {
                        col.DataPropertyName = "name";
                        col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    }

                    _rightGrid.DataSource = _rightData;
                }
            }
        }

        EnableDisableControls();
        UpdateTagTypes();
    }

    private void EnableDisableControls()
    {
        var tagType = GetSelectedTagType();
        var minSortIndex = _tagTypes.Count > 0 ? _tagTypes.Values.Min(x => x.SortIndex) : 0;
        var maxSortIndex = _tagTypes.Count > 0 ? _tagTypes.Values.Max(x => x.SortIndex) : 0;
        _leftRenameGroupButton.Enabled = tagType.HasValue;
        _leftMoveGroupUpButton.Enabled = tagType.HasValue && tagType.Value.SortIndex > minSortIndex;
        _leftMoveGroupDownButton.Enabled = tagType.HasValue && tagType.Value.SortIndex < maxSortIndex;
        _leftDeleteGroupButton.Enabled = tagType.HasValue;

        _rightNewTagButton.Enabled = tagType.HasValue;
        _rightRenameTagButton.Enabled = _rightGrid.SelectedRows.Count == 1;
        _rightDeleteTagButton.Enabled = _rightGrid.SelectedRows.Count > 0;
    }

    private void UpdateTagTypes()
    {
        var selectedTagType = GetSelectedTagType();
        var scrollIndex = _leftGrid.FirstDisplayedScrollingRowIndex;

        _leftData.BeginLoadData();
        try
        {
            _leftData.Rows.Clear();
            _tagTypes = _libraryProvider.GetTagTypes().ToDictionary(x => x.Id);
            foreach (var x in _tagTypes.Values.OrderBy(x => x.SortIndex).ThenBy(x => x.SingularName))
            {
                _leftData.Rows.Add(x.Id, "📁 " + x.PluralName);
            }
        }
        finally
        {
            _leftData.EndLoadData();
        }

        // Restore selected tag type
        _leftGrid.ClearSelection();
        if (scrollIndex >= 0 && scrollIndex < _leftGrid.Rows.Count)
            _leftGrid.FirstDisplayedScrollingRowIndex = scrollIndex;
        if (selectedTagType.HasValue)
        {
            foreach (DataGridViewRow row in _leftGrid.Rows)
            {
                var dataRowView = (DataRowView)row.DataBoundItem!;
                var dataRow = dataRowView.Row;
                var tagTypeId = (TagTypeId)dataRow["id"];
                if (tagTypeId == selectedTagType.Value.Id)
                {
                    row.Selected = true;
                    break;
                }
            }
        }

        UpdateTags();
    }

    private TagType? GetSelectedTagType()
    {
        if (_leftGrid.SelectedRows.Count == 0)
            return null;
        var dataRowView = (DataRowView)_leftGrid.SelectedRows[0].DataBoundItem!;
        var dataRow = dataRowView.Row;
        var tagTypeId = (TagTypeId)dataRow["id"];
        var tagType = _tagTypes[tagTypeId];
        return tagType;
    }

    private void UpdateTags()
    {
        if (_leftGrid.SelectedRows.Count == 0)
        {
            _rightData.Rows.Clear();
            return;
        }

        var tagType = GetSelectedTagType()!.Value;
        var selectedTagIds = GetSelectedTagIds().ToHashSet();
        var scrollIndex = _rightGrid.FirstDisplayedScrollingRowIndex;

        _rightData.BeginLoadData();
        try
        {
            _rightData.Rows.Clear();
            foreach (var x in _libraryProvider.GetTags(tagType.Id))
            {
                _rightData.Rows.Add(x.Id, $"{tagType.PluralName}  🞂  {x.Name}");
            }
        }
        finally
        {
            _rightData.EndLoadData();
        }

        // Restore selected tags.
        _rightGrid.ClearSelection();
        if (scrollIndex >= 0 && scrollIndex < _rightGrid.Rows.Count)
            _rightGrid.FirstDisplayedScrollingRowIndex = scrollIndex;
        foreach (DataGridViewRow row in _rightGrid.Rows)
        {
            var dataRowView = (DataRowView)row.DataBoundItem!;
            var dataRow = dataRowView.Row;
            var tagId = (TagId)dataRow["id"];
            if (selectedTagIds.Contains(tagId))
            {
                row.Selected = true;
            }
        }
    }

    private IEnumerable<TagId> GetSelectedTagIds()
    {
        foreach (DataGridViewRow row in _rightGrid.SelectedRows)
        {
            var dataRowView = (DataRowView)row.DataBoundItem!;
            var dataRow = dataRowView.Row;
            var tagId = (TagId)dataRow["id"];
            yield return tagId;
        }
    }

    private void LeftGrid_SelectionChanged(object? sender, EventArgs e)
    {
        UpdateTags();
        EnableDisableControls();
    }

    private void RightGrid_SelectionChanged(object? sender, EventArgs e)
    {
        EnableDisableControls();
    }

    private void LeftNewGroupButton_Click(object? sender, EventArgs e)
    {
        using var f = _serviceProvider.GetRequiredService<TagTypeForm>();
        f.InitializeNew();
        if (f.ShowDialog(FindForm()) != DialogResult.OK)
            return;

        TagTypeChanged?.Invoke(this, EventArgs.Empty);
        UpdateTagTypes();
    }

    private void LeftGrid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        RenameTagType();
    }

    private void LeftRenameGroupButton_Click(object? sender, EventArgs e)
    {
        RenameTagType();
    }

    private void RenameTagType()
    {
        if (_leftGrid.SelectedRows.Count == 0)
            return;

        var tagType = GetSelectedTagType()!.Value;

        using var f = _serviceProvider.GetRequiredService<TagTypeForm>();
        f.Initialize(tagType);
        if (f.ShowDialog(FindForm()) != DialogResult.OK)
            return;

        TagTypeChanged?.Invoke(this, EventArgs.Empty);
        UpdateTagTypes();
    }

    private void LeftMoveGroupUpButton_Click(object? sender, EventArgs e)
    {
        MoveTagType(-1);
    }

    private void LeftMoveGroupDownButton_Click(object? sender, EventArgs e)
    {
        MoveTagType(1);
    }

    private void MoveTagType(int direction)
    {
        var selectedTagType = GetSelectedTagType()!.Value;
        var tagTypes = _libraryProvider.GetTagTypes().OrderBy(x => x.SortIndex).ThenBy(x => x.SingularName).ToList();

        // Renumber all the sort indices in case they are already messed up before we even start.
        for (var i = 0; i < tagTypes.Count; i++)
        {
            tagTypes[i] = tagTypes[i] with { SortIndex = i };
        }

        var tagTypeIndex = -1;
        for (var i = 0; i < tagTypes.Count; i++)
        {
            if (tagTypes[i].Id == selectedTagType.Id)
            {
                tagTypeIndex = i;
                break;
            }
        }
        Debug.Assert(tagTypeIndex != -1);

        var swapIndex = tagTypeIndex + direction;
        Debug.Assert(swapIndex >= 0 && swapIndex < tagTypes.Count);

        (tagTypes[tagTypeIndex], tagTypes[swapIndex]) = (tagTypes[swapIndex], tagTypes[tagTypeIndex]);

        for (var i = 0; i < tagTypes.Count; i++)
        {
            tagTypes[i] = tagTypes[i] with { SortIndex = i };
        }

        SimpleProgressForm.Do(
            FindForm()!,
            "Moving tag group...",
            async (updateProgress, cancel) =>
            {
                await _libraryProvider.UpdateTagTypesAsync(tagTypes, updateProgress, cancel).ConfigureAwait(true);
            }
        );

        TagTypeChanged?.Invoke(this, EventArgs.Empty);
        UpdateTagTypes();
    }

    private void LeftDeleteGroupButton_Click(object? sender, EventArgs e)
    {
        var tagType = GetSelectedTagType()!.Value;

        var response = MessageBox.Show(
            FindForm(),
            $"Are you sure you want to delete the \"{tagType.PluralName}\" tag group?",
            "Delete",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question
        );
        if (response != DialogResult.OK)
            return;

        SimpleProgressForm.Do(
            FindForm()!,
            "Deleting tag group...",
            async (updateProgress, cancel) =>
            {
                await _libraryProvider.DeleteTagTypeAsync(tagType.Id, updateProgress, cancel).ConfigureAwait(false);
            }
        );

        TagTypeChanged?.Invoke(this, EventArgs.Empty);
        UpdateTagTypes();
    }

    private void RightNewTagButton_Click(object? sender, EventArgs e)
    {
        EditTag(null);
    }

    private void RightRenameTagButton_Click(object? sender, EventArgs e)
    {
        EditTag(GetSelectedTagIds().First());
    }

    private void RightGrid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        EditTag(GetSelectedTagIds().First());
    }

    private void EditTag(TagId? id)
    {
        var tagType = GetSelectedTagType()!.Value;

        using var f = _serviceProvider.GetRequiredService<TagForm>();
        f.Initialize(tagType, id);
        if (f.ShowDialog(FindForm()) == DialogResult.OK)
        {
            TagChanged?.Invoke(this, EventArgs.Empty);
            UpdateTags();
        }
    }

    private void RightDeleteTagButton_Click(object? sender, EventArgs e)
    {
        var tags = GetSelectedTagIds().ToList();
        if (tags.Count == 0)
            return;

        var message =
            tags.Count == 1
                ? "Are you sure you want to delete this tag?"
                : $"Are you sure you want to delete these {tags.Count:#,##0} tags?";

        var response = MessageBox.Show(
            FindForm(),
            message,
            "Delete",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question
        );
        if (response != DialogResult.OK)
            return;

        try
        {
            SimpleProgressForm.Do(
                FindForm()!,
                "Deleting...",
                async (updateProgress, cancel) =>
                {
                    await _libraryProvider.DeleteTagsAsync(tags, updateProgress, cancel).ConfigureAwait(false);
                }
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        TagChanged?.Invoke(this, EventArgs.Empty);
        UpdateTags();
    }
}
