using J.Core.Data;

namespace J.App;

public sealed class EditTagsRenameTagTypeForm : Form
{
    private readonly FlowLayoutPanel _verticalFlow,
        _buttonFlow;
    private readonly TextBox _singularNameText,
        _pluralNameText;
    private readonly Button _okButton,
        _cancelButton;
    private readonly LibraryProviderAdapter _libraryProvider;
    private TagType _tagType;

    public EditTagsRenameTagTypeForm(LibraryProviderAdapter libraryProvider)
    {
        _libraryProvider = libraryProvider;
        Ui ui = new(this);

        Controls.Add(_verticalFlow = ui.NewFlowColumn());
        {
            Control p;

            (p, _singularNameText) = ui.NewLabeledTextBox("&Singular name:", 125);
            _verticalFlow.Controls.Add(p);

            (p, _pluralNameText) = ui.NewLabeledTextBox("&Plural name:", 125);
            _verticalFlow.Controls.Add(p);
            {
                p.Margin = ui.TopSpacing;
            }

            _verticalFlow.Controls.Add(_buttonFlow = ui.NewFlowRow());
            {
                _buttonFlow.Margin = ui.TopSpacingBig;

                _buttonFlow.Controls.Add(_okButton = ui.NewButton("Rename"));
                {
                    _okButton.Click += OkButton_Click;
                    _okButton.Margin += ui.ButtonSpacing;
                }

                _buttonFlow.Controls.Add(_cancelButton = ui.NewButton("Cancel", DialogResult.Cancel));
            }
        }

        Text = "Edit Tags";
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        Padding = ui.DefaultPadding;
        AcceptButton = _okButton;
        CancelButton = _cancelButton;
    }

    public void Initialize(TagType tagType)
    {
        _tagType = tagType;
        _singularNameText.Text = tagType.SingularName;
        _pluralNameText.Text = tagType.PluralName;
        _singularNameText.Focus();
        _singularNameText.SelectAll();
    }

    private void OkButton_Click(object? sender, EventArgs e)
    {
        try
        {
            var singular = _singularNameText.Text;
            var plural = _pluralNameText.Text;

            if (string.IsNullOrWhiteSpace(singular))
                throw new Exception("Please enter a singular name.");

            if (string.IsNullOrWhiteSpace(plural))
                throw new Exception("Please enter a plural name.");

            var newTagType = _tagType with { SingularName = singular, PluralName = plural };

            SimpleProgressForm.Do(
                this,
                "Renaming tag group...",
                async (updateProgress, cancel) =>
                {
                    await _libraryProvider
                        .UpdateTagTypesAsync([newTagType], updateProgress, cancel)
                        .ConfigureAwait(true);
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
}
