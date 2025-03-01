﻿using J.Core.Data;

namespace J.App;

public sealed class TagTypeForm : Form
{
    private readonly FlowLayoutPanel _verticalFlow,
        _buttonFlow;
    private readonly MyTextBox _singularNameText,
        _pluralNameText;
    private readonly Button _okButton,
        _cancelButton;
    private readonly LibraryProviderAdapter _libraryProvider;
    private TagType? _tagType;

    public TagTypeForm(LibraryProviderAdapter libraryProvider)
    {
        _libraryProvider = libraryProvider;
        Ui ui = new(this);

        Controls.Add(_verticalFlow = ui.NewFlowColumn());
        {
            Control p;

            (p, _singularNameText) = ui.NewLabeledTextBox("&Singular name:", 250);
            _verticalFlow.Controls.Add(p);

            (p, _pluralNameText) = ui.NewLabeledTextBox("&Plural name:", 250);
            _verticalFlow.Controls.Add(p);
            {
                p.Margin = ui.TopSpacing;
            }

            _verticalFlow.Controls.Add(_buttonFlow = ui.NewFlowRow());
            {
                _buttonFlow.Dock = DockStyle.Right;
                _buttonFlow.Margin = ui.TopSpacingBig;

                _buttonFlow.Controls.Add(_okButton = ui.NewButton("Rename"));
                {
                    _okButton.Click += OkButton_Click;
                    _okButton.Margin += ui.ButtonSpacing;
                }

                _buttonFlow.Controls.Add(_cancelButton = ui.NewButton("Cancel", DialogResult.Cancel));
            }
        }

        Text = "Tag Group";
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

    public void InitializeNew()
    {
        _tagType = null;
        _singularNameText.Text = "";
        _pluralNameText.Text = "";
        _singularNameText.Focus();
        _singularNameText.SelectAll();
        _okButton.Text = "Create";
    }

    public void Initialize(TagType tagType)
    {
        _tagType = tagType;
        _singularNameText.Text = tagType.SingularName;
        _pluralNameText.Text = tagType.PluralName;
        _singularNameText.Focus();
        _singularNameText.Select(0, 0);
        _okButton.Text = "Rename";
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

            Outcome outcome;
            if (_tagType is null)
            {
                var tagType = new TagType(new(), 0, singular, plural);

                outcome = ProgressForm.Do(
                    this,
                    "Creating tag group...",
                    async (updateProgress, cancel) =>
                    {
                        await _libraryProvider.NewTagTypeAsync(tagType, updateProgress, cancel).ConfigureAwait(false);
                    }
                );
            }
            else
            {
                var newTagType = _tagType.Value with { SingularName = singular, PluralName = plural };

                outcome = ProgressForm.Do(
                    this,
                    "Renaming tag group...",
                    async (updateProgress, cancel) =>
                    {
                        await _libraryProvider
                            .UpdateTagTypesAsync([newTagType], updateProgress, cancel)
                            .ConfigureAwait(false);
                    }
                );
            }

            if (outcome == Outcome.Success)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        catch (Exception ex)
        {
            MessageForm.Show(this, ex, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
