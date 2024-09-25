using J.Core.Data;

namespace J.App;

public sealed class EditTagForm : Form
{
    private readonly LibraryProviderAdapter _libraryProvider;
    private readonly TableLayoutPanel _table;
    private readonly TextBox _nameTextBox;
    private readonly FlowLayoutPanel _leftButtonFlow,
        _rightButtonFlow;
    private readonly Button _deleteButton,
        _saveButton,
        _cancelButton;
    private TagType _type;
    private TagId? _id;

    public EditTagForm(LibraryProviderAdapter libraryProvider)
    {
        _libraryProvider = libraryProvider;
        Ui ui = new(this);

        Controls.Add(_table = ui.NewTable(2, 2));
        {
            _nameTextBox = ui.AddPairToTable(_table, 0, 0, ui.NewLabeledTextBox("&Name:", 300), 2);

            _table.Controls.Add(_leftButtonFlow = ui.NewFlowRow(), 0, 1);
            {
                _leftButtonFlow.Margin = ui.TopSpacingBig;

                _leftButtonFlow.Controls.Add(_deleteButton = ui.NewButton("Delete"));
                {
                    _deleteButton.Click += DeleteButton_Click;
                }
            }

            _table.Controls.Add(_rightButtonFlow = ui.NewFlowRow(), 1, 1);
            {
                _rightButtonFlow.Margin = ui.TopSpacingBig;
                _rightButtonFlow.Dock = DockStyle.Right;

                _rightButtonFlow.Controls.Add(_saveButton = ui.NewButton("Save"));
                {
                    _saveButton.Click += SaveButton_Click;
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

    private void DeleteButton_Click(object? sender, EventArgs e)
    {
        var response = MessageBox.Show(
            this,
            $"Are you sure you want to delete this {_type.SingularName.ToLower()}?",
            "Delete",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question
        );
        if (response != DialogResult.OK)
            return;

        try
        {
            SimpleProgressForm.Do(
                this,
                "Deleting tag...",
                async (cancel) =>
                {
                    await _libraryProvider.DeleteTagAsync(_id!, cancel).ConfigureAwait(false);
                }
            );

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        try
        {
            SimpleProgressForm.Do(
                this,
                "Saving tag...",
                async (cancel) =>
                {
                    if (_id is null)
                    {
                        _id = new();
                        Tag tag = new(_id, _type.Id, _nameTextBox.Text);
                        await _libraryProvider.NewTagAsync(tag, cancel).ConfigureAwait(true);
                    }
                    else
                    {
                        Tag tag = new(_id, _type.Id, _nameTextBox.Text);
                        await _libraryProvider.UpdateTagAsync(tag, cancel).ConfigureAwait(true);
                    }
                }
            );

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public void OpenTag(TagType type, TagId? id)
    {
        Text = $"{(id is null ? "New" : "Edit")} {type.SingularName}";
        _type = type;
        _id = id;
        _deleteButton.Visible = _id is not null;
        if (id is not null)
        {
            var tag = _libraryProvider.GetTag(id);
            _nameTextBox.Text = tag.Name;
        }
    }
}
