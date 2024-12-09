using System.ComponentModel;
using System.Diagnostics;
using J.Core.Data;

namespace J.App;

public sealed class FilterChooseTagForm : Form
{
    private readonly LibraryProviderAdapter _libraryProvider;
    private readonly TableLayoutPanel _table;
    private readonly MyTextBox _searchText;
    private readonly DataGridView _grid;
    private readonly FlowLayoutPanel _buttonFlow;
    private readonly Button _okButton,
        _cancelButton;
    private List<Tag> _tags = [];

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public List<TagId> SelectedTags { get; private set; } = [];

    public FilterChooseTagForm(LibraryProviderAdapter libraryProvider)
    {
        _libraryProvider = libraryProvider;
        Ui ui = new(this);

        Controls.Add(_table = ui.NewTable(1, 3));
        {
            _table.Padding = ui.DefaultPadding;
            _table.RowStyles[1].SizeType = SizeType.Percent;
            _table.RowStyles[1].Height = 100;

            _table.Controls.Add(ui.NewLabeledPair("Search:", _searchText = ui.NewTextBox(200)), 0, 0);
            {
                _searchText.TextChanged += SearchText_TextChanged;
            }

            _table.Controls.Add(_grid = ui.NewDataGridView(), 0, 1);
            {
                _grid.Margin = ui.TopSpacing;
                _grid.BorderStyle = BorderStyle.Fixed3D;
                _grid.Columns.Add("name", "Name");
                _grid.Columns["name"]!.DataPropertyName = "name";
                _grid.Columns["name"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _grid.ColumnHeadersVisible = false;
                _grid.CellDoubleClick += Grid_CellDoubleClick;
            }

            _table.Controls.Add(_buttonFlow = ui.NewFlowRow(), 0, 2);
            {
                _buttonFlow.Margin = ui.TopSpacingBig;
                _buttonFlow.Dock = DockStyle.Right;

                _buttonFlow.Controls.Add(_okButton = ui.NewButton("OK"));
                _okButton.Click += OkButton_Click;
                _okButton.Margin += ui.ButtonSpacing;

                _buttonFlow.Controls.Add(_cancelButton = ui.NewButton("Cancel", DialogResult.Cancel));
            }
        }

        StartPosition = FormStartPosition.CenterParent;
        Size = ui.GetSize(300, 400);
        MinimumSize = ui.GetSize(300, 400);
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        AcceptButton = _okButton;
        CancelButton = _cancelButton;
    }

    public void Initialize(TagType tagType, FilterOperator filterOperator)
    {
        Text = $"{tagType.SingularName} {filterOperator.GetDisplayName(true)}";

        _tags = _libraryProvider.GetTags(tagType.Id).OrderBy(x => x.Name).ToList();
        UpdateList();
    }

    private void SearchText_TextChanged(object? sender, EventArgs e)
    {
        UpdateList();
    }

    private void UpdateList()
    {
        var search = _searchText.Text;

        var words = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            _grid.DataSource = _tags;
            return;
        }

        List<Tag> filteredTags = new(_tags.Count);
        foreach (var tag in _tags)
        {
            var match = false;
            foreach (var word in words)
            {
                if (tag.Name.Contains(word, StringComparison.OrdinalIgnoreCase))
                {
                    match = true;
                    break;
                }
            }

            if (match)
                filteredTags.Add(tag);
        }

        _grid.DataSource = filteredTags;
    }

    private void OkButton_Click(object? sender, EventArgs e)
    {
        if (_grid.SelectedCells.Count == 0)
        {
            MessageBox.Show(Text, "Please select a tag.", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            return;
        }

        Ok();
    }

    private void Grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (_grid.SelectedCells.Count > 0)
            Ok();
    }

    private void Ok()
    {
        Debug.Assert(_grid.SelectedCells.Count > 0);

        SelectedTags = (
            from DataGridViewCell cell in _grid.SelectedCells
            select ((Tag)cell.OwningRow!.DataBoundItem!).Id
        )
            .Distinct()
            .ToList();

        DialogResult = DialogResult.OK;
        Close();
    }
}
