using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace J.App;

public sealed class ConvertMoviesForm : Form
{
    private readonly TableLayoutPanel _table;
    private readonly FlowLayoutPanel _topFlow1,
        _topFlow2,
        _bottomFlow;
    private readonly Label _helpLabel;
    private readonly ListBox _filesList;
    private readonly TextBox _outputDirText;
    private readonly ComboBox _qualityCombo,
        _speedCombo;
    private readonly Button _okButton,
        _cancelButton;

    public ConvertMoviesForm()
    {
        Control p;
        Ui ui = new(this);

        Controls.Add(_table = ui.NewTable(1, 5));
        {
            _table.Padding = ui.DefaultPadding;
            _table.RowStyles[3].SizeType = SizeType.Percent;
            _table.RowStyles[3].Height = 100;

            _table.Controls.Add(_topFlow1 = ui.NewFlowRow(), 0, 0);
            {
                _topFlow1.Padding += ui.BottomSpacing;

                (p, _outputDirText) = ui.NewLabeledOpenFolderTextBox(
                    "&Output folder:",
                    400,
                    d =>
                    {
                        d.ShowNewFolderButton = true;
                    }
                );
                _topFlow1.Controls.Add(p);
            }

            _table.Controls.Add(_topFlow2 = ui.NewFlowRow(), 0, 1);
            {
                _topFlow2.Padding += ui.BottomSpacing;

                _qualityCombo = _topFlow2.AddPair(ui.NewLabeledPair("Quality (lower is better):", ui.NewDropDown(200)));
                {
                    _qualityCombo.Margin += ui.RightSpacing;

                    List<string> qualities = [];
                    for (var i = 0; i <= 28; i++)
                        qualities.Add(i.ToString());

                    qualities[0] = "0 (lossless)";
                    qualities[17] = "17 (recommended)";
                    qualities[28] = "28 (worst quality)";

                    foreach (var quality in qualities)
                        _qualityCombo.Items.Add(quality);

                    _qualityCombo.SelectedIndex = 17;
                }

                _speedCombo = _topFlow2.AddPair(
                    ui.NewLabeledPair("Encoding speed (slower is better):", ui.NewDropDown(200))
                );
                {
                    List<string> speeds =
                    [
                        "ultrafast",
                        "superfast",
                        "veryfast",
                        "faster",
                        "fast",
                        "medium",
                        "slow (recommended)",
                        "slower",
                        "veryslow",
                    ];
                    foreach (var speed in speeds)
                        _speedCombo.Items.Add(speed);
                    _speedCombo.SelectedIndex = speeds.IndexOf("slow (recommended)");
                }
            }

            _table.Controls.Add(_helpLabel = ui.NewLabel("Drag-and-drop movie files below."), 0, 2);
            {
                _helpLabel.Padding += ui.GetPadding(0, 0, 0, 2);
            }

            _table.Controls.Add(_filesList = ui.NewListBox(), 0, 3);

            _table.Controls.Add(_bottomFlow = ui.NewFlowRow(), 0, 4);
            {
                _bottomFlow.Padding += ui.TopSpacingBig;
                _bottomFlow.Dock = DockStyle.Right;

                _bottomFlow.Controls.Add(_okButton = ui.NewButton("Start"));

                _bottomFlow.Controls.Add(_cancelButton = ui.NewButton("Close"));
                {
                    _cancelButton.Click += delegate
                    {
                        Close();
                    };
                }
            }
        }

        Text = "Convert Movies to MP4";
        StartPosition = FormStartPosition.CenterScreen;
        Size = ui.GetSize(500, 500);
        MinimumSize = ui.GetSize(500, 400);
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = false;
        AcceptButton = _okButton;
        CancelButton = _cancelButton;
        Icon = ui.GetIconResource("App.ico");
        ShowIcon = true;
        ShowInTaskbar = true;
    }
}
