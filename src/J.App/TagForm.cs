using J.Core.Data;

namespace J.App;

public sealed class TagForm : Form
{
    private readonly LibraryProviderAdapter _libraryProvider;
    private readonly TableLayoutPanel _table;
    private readonly MyTextBox _nameTextBox;
    private readonly FlowLayoutPanel _rightButtonFlow;
    private readonly Button _saveButton,
        _cancelButton;
    private TagType _type;
    private TagId? _id;

    public TagForm(LibraryProviderAdapter libraryProvider)
    {
        _libraryProvider = libraryProvider;
        Ui ui = new(this);

        Controls.Add(_table = ui.NewTable(2, 2));
        {
            _nameTextBox = _table.AddPair(0, 0, ui.NewLabeledTextBox("&Name:", 300), 2);

            _table.Controls.Add(_rightButtonFlow = ui.NewFlowRow(), 1, 1);
            {
                _rightButtonFlow.Margin = ui.TopSpacingBig;
                _rightButtonFlow.Dock = DockStyle.Right;

                _rightButtonFlow.Controls.Add(_saveButton = ui.NewButton("Save"));
                {
                    _saveButton.Click += SaveButton_Click;
                    _saveButton.Margin += ui.ButtonSpacing;
                }

                _rightButtonFlow.Controls.Add(_cancelButton = ui.NewButton("Cancel", DialogResult.Cancel));
            }
        }

        AcceptButton = _saveButton;
        CancelButton = _cancelButton;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Edit Tag";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        Padding = ui.DefaultPadding;
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
                throw new Exception("Please enter a name.");

            var outcome = ProgressForm.Do(
                this,
                "Saving tag...",
                async (updateProgress, cancel) =>
                {
                    if (_id is null)
                    {
                        _id = new();
                        Tag tag = new(_id, _type.Id, _nameTextBox.Text);
                        await _libraryProvider.NewTagAsync(tag, updateProgress, cancel).ConfigureAwait(true);
                    }
                    else
                    {
                        Tag tag = new(_id, _type.Id, _nameTextBox.Text);
                        await _libraryProvider.UpdateTagAsync(tag, updateProgress, cancel).ConfigureAwait(true);
                    }
                }
            );

            if (outcome == Outcome.Success)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            MessageForm.Show(this, ex, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public void Initialize(TagType type, TagId? id)
    {
        Text = $"{(id is null ? "New" : "Edit")} {type.SingularName}";
        _type = type;
        _id = id;
        _saveButton.Text = id is null ? "Create" : "Rename";
        if (id is not null)
        {
            var tag = _libraryProvider.GetTag(id);
            _nameTextBox.Text = tag.Name;
            _nameTextBox.Select(0, 0);
        }
    }
}
