using J.Core;
using J.Core.Data;

namespace J.App;

public sealed class OptionsForm : Form
{
    private readonly Preferences _preferences;
    private readonly TableLayoutPanel _table;
    private readonly FlowLayoutPanel _buttonFlow,
        _generalFlow;
    private readonly TabControl _tabControl;
    private readonly TabPage _generalTab;
    private readonly ComboBox _vlcCombo,
        _windowMaximizeBehaviorCombo;
    private readonly Button _okButton,
        _cancelButton;

    // VLC installation to use for playback
    private readonly List<VlcInstallationToUse> _vlcValues =
    [
        VlcInstallationToUse.Automatic,
        VlcInstallationToUse.Bundled,
        VlcInstallationToUse.System,
    ];
    private readonly string[] _vlcNames = ["Automatic (recommended)", "Bundled with Jackpot", "External install"];

    // Window maximize behavior
    private readonly List<WindowMaximizeBehavior> _windowMaximizeBehaviorValues =
    [
        WindowMaximizeBehavior.Fullscreen,
        WindowMaximizeBehavior.Windowed,
    ];
    private readonly string[] _windowMaximizeBehaviorNames = ["Fullscreen", "Windowed"];

    public OptionsForm(Preferences preferences)
    {
        _preferences = preferences;
        Ui ui = new(this);

        Controls.Add(_table = ui.NewTable(1, 2));
        {
            _table.RowStyles[0].SizeType = SizeType.Percent;
            _table.RowStyles[0].Height = 100;

            _table.Controls.Add(_tabControl = ui.NewTabControl(100), 0, 0);
            {
                _tabControl.TabPages.Add(_generalTab = ui.NewTabPage("General"));
                {
                    _generalTab.Controls.Add(_generalFlow = ui.NewFlowColumn());
                    {
                        _generalFlow.Padding = ui.DefaultPadding;

                        _generalFlow.Controls.Add(
                            ui.NewLabeledPair("&VLC installation to use for playback:", _vlcCombo = ui.NewDropDown(200))
                        );
                        {
                            _vlcCombo.Margin += ui.BottomSpacing;
                            _vlcCombo.Items.AddRange(_vlcNames);
                            var value = preferences.GetEnum<VlcInstallationToUse>(
                                Preferences.Key.Shared_VlcInstallationToUse
                            );
                            _vlcCombo.SelectedIndex = _vlcValues.IndexOf(value);
                        }

                        _generalFlow.Controls.Add(
                            ui.NewLabeledPair(
                                "Window &maximize behavior:",
                                _windowMaximizeBehaviorCombo = ui.NewDropDown(200)
                            )
                        );
                        {
                            _windowMaximizeBehaviorCombo.Items.AddRange(_windowMaximizeBehaviorNames);
                            var value = preferences.GetEnum<WindowMaximizeBehavior>(
                                Preferences.Key.MainForm_WindowMaximizeBehavior
                            );
                            _windowMaximizeBehaviorCombo.SelectedIndex = _windowMaximizeBehaviorValues.IndexOf(value);
                        }
                    }
                }
            }

            _table.Controls.Add(_buttonFlow = ui.NewFlowRow(), 0, 1);
            {
                _buttonFlow.Padding = ui.TopSpacing;
                _buttonFlow.Dock = DockStyle.Right;

                _buttonFlow.Controls.Add(_okButton = ui.NewButton("OK"));
                {
                    _okButton.Click += OkButton_Click;
                }

                _buttonFlow.Controls.Add(_cancelButton = ui.NewButton("Cancel", DialogResult.Cancel));
            }
        }

        Text = "Options";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = Size = ui.GetSize(400, 400);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        AcceptButton = _okButton;
        CancelButton = _cancelButton;
        ShowIcon = false;
        ShowInTaskbar = false;
        Padding = ui.DefaultPadding;
    }

    private void OkButton_Click(object? sender, EventArgs e)
    {
        _preferences.WithTransaction(() =>
        {
            _preferences.SetEnum(Preferences.Key.Shared_VlcInstallationToUse, _vlcValues[_vlcCombo.SelectedIndex]);
            _preferences.SetEnum(
                Preferences.Key.MainForm_WindowMaximizeBehavior,
                _windowMaximizeBehaviorValues[_windowMaximizeBehaviorCombo.SelectedIndex]
            );
        });
        DialogResult = DialogResult.OK;
        Close();
    }
}
