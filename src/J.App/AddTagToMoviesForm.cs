using J.Core.Data;

namespace J.App;

public sealed class AddTagToMoviesForm : Form
{
    private readonly LibraryProviderAdapter _libraryProvider;
    private readonly TableLayoutPanel _table;
    private readonly ListBox _listBox;
    private readonly FlowLayoutPanel _buttonFlow;
    private readonly Button _okButton,
        _cancelButton;
    private readonly List<Tag> _tags = [];
    private readonly List<MovieId> _movieIds = [];

    public AddTagToMoviesForm(LibraryProviderAdapter libraryProvider)
    {
        _libraryProvider = libraryProvider;
        Ui ui = new(this);

        Controls.Add(_table = ui.NewTable(1, 2));
        {
            _table.Padding = ui.DefaultPadding;
            _table.RowStyles[0].SizeType = SizeType.Percent;
            _table.RowStyles[0].Height = 100;

            _table.Controls.Add(_listBox = ui.NewListBox(), 0, 0);
            {
                _listBox.DoubleClick += ListBox_DoubleClick;
                var tagTypes = _libraryProvider.GetTagTypes().ToDictionary(x => x.Id);

                foreach (
                    var (label, tag) in from tag in _libraryProvider.GetTags()
                    let tagType = tagTypes[tag.TagTypeId]
                    orderby tagType.SortIndex, tagType.SingularName, tag.Name
                    select ($"{tagType.SingularName}: {tag.Name}", tag)
                )
                {
                    _tags.Add(tag);
                    _listBox.Items.Add(tag.Name);
                }
            }

            _table.Controls.Add(_buttonFlow = ui.NewFlowRow(), 0, 1);
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

        StartPosition = FormStartPosition.CenterParent;
        Size = ui.GetSize(300, 500);
        MinimumSize = ui.GetSize(300, 200);
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
    }

    private void ListBox_DoubleClick(object? sender, EventArgs e)
    {
        if (_listBox.SelectedIndex < 0)
            return;

        Ok();
    }

    private void OkButton_Click(object? sender, EventArgs e)
    {
        if (_listBox.SelectedIndex < 0)
        {
            MessageBox.Show("Please select a tag.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Ok();
    }

    private void Ok()
    {
        var tag = _tags[_listBox.SelectedIndex].Id;

        List<(MovieId MovieId, TagId TagId)> movieTags = [];
        foreach (var movieId in _movieIds)
            movieTags.Add((movieId, tag));

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
}
