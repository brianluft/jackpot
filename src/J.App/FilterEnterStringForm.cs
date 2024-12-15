using System.ComponentModel;
using J.Core;
using J.Core.Data;

namespace J.App;

public sealed class FilterEnterStringForm : Form
{
    private readonly TableLayoutPanel _table;
    private readonly MyTextBox _textBox;
    private readonly FlowLayoutPanel _buttonFlow;
    private readonly Button _okButton,
        _cancelButton;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string SelectedString { get; private set; } = "";

    public FilterEnterStringForm()
    {
        Ui ui = new(this);

        Controls.Add(_table = ui.NewTable(1, 2));
        {
            _table.Padding = ui.DefaultPadding;

            _textBox = _table.AddPair(0, 0, ui.NewLabeledTextBox("Filter phrase:", 200));

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

        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        AcceptButton = _okButton;
        CancelButton = _cancelButton;
        ShowIcon = false;
        ShowInTaskbar = false;
    }

    public void Initialize(FilterField filterField, FilterOperator filterOperator)
    {
        Text = $"{filterField.DisplayName} {filterOperator.GetDisplayName(true)}";
    }

    private void OkButton_Click(object? sender, EventArgs e)
    {
        SelectedString = _textBox.Text;
        DialogResult = DialogResult.OK;
        Close();
    }
}
