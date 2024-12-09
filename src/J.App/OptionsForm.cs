using J.Core;
using J.Core.Data;

namespace J.App;

public sealed class OptionsForm : Form
{
    private readonly Preferences _preferences;
    private readonly TableLayoutPanel _table;
    private readonly FlowLayoutPanel _buttonFlow,
        _libraryFlow,
        _m3u8Flow;
    private readonly TabControl _tabControl;
    private readonly TabPage _libraryTab,
        _m3u8Page;
    private readonly ComboBox _playerCombo,
        _windowMaximizeBehaviorCombo,
        _columnCountCombo;
    private readonly Button _okButton,
        _cancelButton;
    private readonly CheckBox _enableM3u8FolderCheck,
        _exitConfirmationCheck;
    private readonly MyTextBox _m3u8FolderText,
        _m3u8HostnameText;

    // Movie player to use for playback
    private readonly List<MoviePlayerToUse> _playerValues =
    [
        MoviePlayerToUse.Automatic,
        MoviePlayerToUse.Integrated,
        MoviePlayerToUse.WebBrowser,
        MoviePlayerToUse.Vlc,
    ];
    private readonly string[] _playerNames = ["Automatic", "Integrated player", "Web browser", "VLC"];

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

            _table.Controls.Add(_tabControl = ui.NewTabControl(200), 0, 0);
            {
                _tabControl.TabPages.Add(_libraryTab = ui.NewTabPage("Library"));
                {
                    _libraryTab.Controls.Add(_libraryFlow = ui.NewFlowColumn());
                    {
                        _libraryFlow.Padding = ui.DefaultPadding;

                        _libraryFlow.Controls.Add(
                            ui.NewLabeledPair("Thumbnail &columns:", _columnCountCombo = ui.NewDropDownList(200))
                        );
                        {
                            _columnCountCombo.Margin += ui.BottomSpacing;
                            _columnCountCombo.Items.AddRange(
                                Enumerable.Range(1, 8).Select(i => i.ToString()).ToArray()
                            );
                            _columnCountCombo.SelectedIndex =
                                (int)preferences.GetInteger(Preferences.Key.Shared_ColumnCount) - 1;
                        }

                        _libraryFlow.Controls.Add(
                            ui.NewLabeledPair("Movie &player app:", _playerCombo = ui.NewDropDownList(200))
                        );
                        {
                            _playerCombo.Margin += ui.BottomSpacing;
                            _playerCombo.Items.AddRange(_playerNames);
                            var value = preferences.GetEnum<MoviePlayerToUse>(Preferences.Key.Shared_MoviePlayerToUse);
                            _playerCombo.SelectedIndex = _playerValues.IndexOf(value);
                        }

                        _libraryFlow.Controls.Add(
                            ui.NewLabeledPair(
                                "&Maximized window style:",
                                _windowMaximizeBehaviorCombo = ui.NewDropDownList(200)
                            )
                        );
                        {
                            _windowMaximizeBehaviorCombo.Margin += ui.BottomSpacing;
                            _windowMaximizeBehaviorCombo.Items.AddRange(_windowMaximizeBehaviorNames);
                            var value = preferences.GetEnum<WindowMaximizeBehavior>(
                                Preferences.Key.MainForm_WindowMaximizeBehavior
                            );
                            _windowMaximizeBehaviorCombo.SelectedIndex = _windowMaximizeBehaviorValues.IndexOf(value);
                        }

                        _libraryFlow.Controls.Add(
                            _exitConfirmationCheck = ui.NewCheckBox("Prompt to confirm when exiting the app")
                        );
                        {
                            _exitConfirmationCheck.Margin += ui.BottomSpacing;
                            _exitConfirmationCheck.Checked =
                                preferences.GetInteger(Preferences.Key.MainForm_ExitConfirmation) != 0;
                        }
                    }
                }

                _tabControl.TabPages.Add(_m3u8Page = ui.NewTabPage("Network Sharing"));
                {
                    var m3u8Settings = preferences.GetJson<M3u8SyncSettings>(Preferences.Key.M3u8FolderSync_Settings);

                    _m3u8Page.Controls.Add(_m3u8Flow = ui.NewFlowColumn());
                    {
                        _m3u8Flow.Padding = ui.DefaultPadding;

                        _m3u8Flow.Controls.Add(
                            ui.NewLabel(
                                "Jackpot can maintain a folder of M3U8 playlist files for non-Windows\ndevices to play via Windows file sharing."
                            )
                        );

                        _m3u8Flow.Controls.Add(
                            _enableM3u8FolderCheck = ui.NewCheckBox("Store M3U8 files in a local folder")
                        );
                        {
                            _enableM3u8FolderCheck.Margin += ui.TopSpacingBig + ui.BottomSpacing;
                            _enableM3u8FolderCheck.Checked = m3u8Settings.EnableLocalM3u8Folder;
                        }

                        Control p;
                        (p, _m3u8FolderText) = ui.NewLabeledOpenFolderTextBox("Folder:", 400, _ => { });
                        {
                            _m3u8Flow.Controls.Add(p);
                            p.Margin = ui.BottomSpacing;
                            _m3u8FolderText.Text = m3u8Settings.LocalM3u8FolderPath;
                        }

                        (p, _m3u8HostnameText) = ui.NewLabeledTextBox("Host or IP address to use in M3U8 files:", 300);
                        {
                            _m3u8Flow.Controls.Add(p);
                            _m3u8HostnameText.Text = m3u8Settings.M3u8Hostname;
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
                    _okButton.Margin += ui.ButtonSpacing;
                }

                _buttonFlow.Controls.Add(_cancelButton = ui.NewButton("Cancel", DialogResult.Cancel));
            }
        }

        Text = "Options";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = Size = ui.GetSize(500, 450);
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
        try
        {
            if (_enableM3u8FolderCheck.Checked)
            {
                if (string.IsNullOrWhiteSpace(_m3u8FolderText.Text))
                    throw new Exception("Please enter a folder for .M3U8 files.");
            }

            M3u8SyncSettings m3u8SyncSettings =
                new(_enableM3u8FolderCheck.Checked, _m3u8FolderText.Text, _m3u8HostnameText.Text);

            _preferences.WithTransaction(() =>
            {
                _preferences.SetEnum(
                    Preferences.Key.Shared_MoviePlayerToUse,
                    _playerValues[_playerCombo.SelectedIndex]
                );
                _preferences.SetEnum(
                    Preferences.Key.MainForm_WindowMaximizeBehavior,
                    _windowMaximizeBehaviorValues[_windowMaximizeBehaviorCombo.SelectedIndex]
                );
                _preferences.SetInteger(Preferences.Key.Shared_ColumnCount, _columnCountCombo.SelectedIndex + 1);
                _preferences.SetJson(Preferences.Key.M3u8FolderSync_Settings, m3u8SyncSettings);
                _preferences.SetBoolean(Preferences.Key.MainForm_ExitConfirmation, _exitConfirmationCheck.Checked);
            });

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
