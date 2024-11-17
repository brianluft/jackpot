using System.ComponentModel;

namespace J.App;

public sealed class EditMoviesRenameMovieForm : Form
{
    private readonly TableLayoutPanel _table;
    private readonly TextBox _oldText,
        _newText;
    private readonly FlowLayoutPanel _buttonFlow;
    private readonly Button _okButton,
        _cancelButton;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string NewName { get; private set; } = "";

    public EditMoviesRenameMovieForm(string name)
    {
        Ui ui = new(this);

        Controls.Add(_table = ui.NewTable(1, 3));
        {
            Control p;

            (p, _oldText) = ui.NewLabeledTextBox("&Old name:", 400);
            _table.Controls.Add(p, 0, 0);
            {
                _oldText.ReadOnly = true;
                _oldText.Text = name;
            }

            (p, _newText) = ui.NewLabeledTextBox("&New name:", 400);
            _table.Controls.Add(p, 0, 1);
            {
                p.Margin = ui.TopSpacing;
                _newText.Text = name;
            }

            _table.Controls.Add(_buttonFlow = ui.NewFlowRow());
            {
                _buttonFlow.Margin = ui.TopSpacingBig;

                _buttonFlow.Controls.Add(_okButton = ui.NewButton("OK"));
                {
                    _okButton.Click += OkButton_Click;
                    _okButton.Margin += ui.ButtonSpacing;
                }

                _buttonFlow.Controls.Add(_cancelButton = ui.NewButton("Cancel", DialogResult.Cancel));
            }
        }

        _newText.Select();

        AcceptButton = _okButton;
        CancelButton = _cancelButton;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Rename Movie";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        Padding = ui.DefaultPadding;
    }

    private void OkButton_Click(object? sender, EventArgs e)
    {
        NewName = _newText.Text;
        DialogResult = DialogResult.OK;
        Close();
    }
}
