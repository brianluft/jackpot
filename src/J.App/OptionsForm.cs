using System.Diagnostics;
using J.Core;
using J.Core.Data;

namespace J.App;

public sealed class OptionsForm : Form
{
    private readonly Preferences _preferences;
    private readonly TableLayoutPanel _table;
    private readonly FlowLayoutPanel _buttonFlow,
        _libraryFlow,
        _sharingFlow,
        _shareTypesFlow,
        _shareVlcFlow,
        _shareBrowserFlow,
        _browserFlow;
    private readonly TabControl _tabControl;
    private readonly TabPage _libraryTab,
        _sharingPage;
    private readonly ComboBox _playerCombo,
        _windowMaximizeBehaviorCombo,
        _columnCountCombo;
    private readonly Button _okButton,
        _cancelButton,
        _browserAddressCopyButton;
    private readonly CheckBox _shareVlcCheck,
        _exitConfirmationCheck,
        _shareBrowserCheck;
    private readonly MyTextBox _m3u8FolderText,
        _hostnameText,
        _browserAddressText;
    private readonly LinkLabel _wikiLink;
    private readonly List<Control> _shareVlcControls = [];
    private readonly List<Control> _shareBrowserControls = [];

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
        Control p;

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
                            ui.NewLabeledPair("Grid &columns:", _columnCountCombo = ui.NewDropDownList(200))
                        );
                        {
                            _columnCountCombo.Margin += ui.BottomSpacing;
                            _columnCountCombo.Items.AddRange(
                                Enumerable.Range(1, 5).Select(i => i.ToString()).ToArray()
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
                            _exitConfirmationCheck = ui.NewCheckBox("Confirm when e&xiting the app")
                        );
                        {
                            _exitConfirmationCheck.Margin += ui.BottomSpacing;
                            _exitConfirmationCheck.Checked =
                                preferences.GetInteger(Preferences.Key.MainForm_ExitConfirmation) != 0;
                        }
                    }
                }

                _tabControl.TabPages.Add(_sharingPage = ui.NewTabPage("Network Sharing"));
                {
                    _sharingPage.Controls.Add(_sharingFlow = ui.NewFlowColumn());
                    {
                        _sharingFlow.Padding = ui.DefaultPadding;

                        _sharingFlow.Controls.Add(
                            _wikiLink = ui.NewLinkLabel("Read the Jackpot wiki for more information on network sharing")
                        );
                        {
                            _wikiLink.Margin = Padding.Empty;
                            _wikiLink.Click += WikiLink_Click;
                        }

                        (p, _hostnameText) = ui.NewLabeledTextBox("This PC's IP &address or hostname:", 200);
                        {
                            p.Margin += ui.TopSpacingBig;
                            _sharingFlow.Controls.Add(p);
                            var host = preferences.GetText(Preferences.Key.NetworkSharing_Hostname);

                            if (string.IsNullOrWhiteSpace(host) || host == "localhost")
                            {
                                host = LanIpFinder.GetLanIpOrEmptyString();
                            }

                            _hostnameText!.Text = host;
                            _hostnameText.TextChanged += HostnameText_TextChanged;
                        }

                        _sharingFlow.Controls.Add(_shareTypesFlow = ui.NewFlowRow());
                        {
                            _shareTypesFlow.Controls.Add(_shareVlcFlow = ui.NewFlowColumn());
                            {
                                _shareVlcFlow.Controls.Add(_shareVlcCheck = ui.NewCheckBox("Allow &VLC access"));
                                {
                                    _shareVlcCheck.Margin += ui.TopSpacingBig + ui.BottomSpacing;
                                    _shareVlcCheck.Checked = preferences.GetBoolean(
                                        Preferences.Key.NetworkSharing_AllowVlcAccess
                                    );
                                    _shareVlcCheck.CheckedChanged += ShareVlcCheck_CheckedChanged;
                                }

                                _shareVlcFlow.Controls.Add(
                                    p = ui.NewLabel(
                                        """
                                        Jackpot will create an .M3U8 file for each movie.
                                        If you share this folder using Windows file sharing
                                        (SMB), the VLC app can play the files.
                                        """
                                    )
                                );
                                {
                                    p.Margin += ui.BottomSpacing + ui.GetPadding(18, 0, 0, 0);
                                    _shareVlcControls.Add(p);
                                }

                                (p, _m3u8FolderText) = ui.NewLabeledOpenFolderTextBox("&Folder:", 325, _ => { });
                                {
                                    _shareVlcFlow.Controls.Add(p);
                                    p.Margin += ui.BottomSpacing + ui.GetPadding(18, 0, 0, 0);
                                    _m3u8FolderText.Text = preferences.GetText(
                                        Preferences.Key.NetworkSharing_VlcFolderPath
                                    );
                                    _shareVlcControls.Add(p);
                                }
                            }

                            _shareTypesFlow.Controls.Add(_shareBrowserFlow = ui.NewFlowColumn());
                            {
                                _shareBrowserFlow.Margin += ui.GetPadding(15, 0, 0, 0);

                                _shareBrowserFlow.Controls.Add(
                                    _shareBrowserCheck = ui.NewCheckBox("Allow &web browser access")
                                );
                                {
                                    _shareBrowserCheck.Margin += ui.TopSpacingBig;
                                    _shareBrowserCheck.CheckedChanged += ShareBrowserCheck_CheckedChanged;
                                    _shareBrowserCheck.Checked = preferences.GetBoolean(
                                        Preferences.Key.NetworkSharing_AllowWebBrowserAccess
                                    );
                                }

                                _shareBrowserFlow.Controls.Add(
                                    p = ui.NewLabeledPair("A&ddress:", _browserFlow = ui.NewFlowColumn())
                                );
                                {
                                    p.Margin += ui.TopSpacing + ui.GetPadding(18, 0, 0, 0);

                                    _browserFlow.Controls.Add(_browserAddressText = ui.NewTextBox(280));
                                    {
                                        _browserAddressText.Text = "";
                                        _browserAddressText.ReadOnly = true;
                                    }

                                    _browserFlow.Controls.Add(_browserAddressCopyButton = ui.NewButton("Copy"));
                                    {
                                        _browserAddressCopyButton.Dock = DockStyle.Right;
                                        _browserAddressCopyButton.Click += BrowserAddressCopyButton_Click;
                                    }

                                    _shareBrowserControls.Add(p);
                                }
                            }
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
        Size = ui.GetSize(765, 575);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        AcceptButton = _okButton;
        CancelButton = _cancelButton;
        ShowIcon = false;
        ShowInTaskbar = false;
        Padding = ui.DefaultPadding;

        UpdateShareTab();
    }

    private void OkButton_Click(object? sender, EventArgs e)
    {
        try
        {
            if ((_shareVlcCheck.Checked || _shareBrowserCheck.Checked) && string.IsNullOrWhiteSpace(_hostnameText.Text))
            {
                throw new Exception("Please enter a hostname or IP address.");
            }

            if (_shareVlcCheck.Checked)
            {
                if (string.IsNullOrWhiteSpace(_m3u8FolderText.Text))
                    throw new Exception("Please enter a folder for .M3U8 files.");
            }

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
                _preferences.SetBoolean(Preferences.Key.MainForm_ExitConfirmation, _exitConfirmationCheck.Checked);
                _preferences.SetText(Preferences.Key.NetworkSharing_Hostname, _hostnameText.Text);
                _preferences.SetBoolean(Preferences.Key.NetworkSharing_AllowVlcAccess, _shareVlcCheck.Checked);
                _preferences.SetText(Preferences.Key.NetworkSharing_VlcFolderPath, _m3u8FolderText.Text);
                _preferences.SetBoolean(
                    Preferences.Key.NetworkSharing_AllowWebBrowserAccess,
                    _shareBrowserCheck.Checked
                );
            });

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageForm.Show(this, ex, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BrowserAddressCopyButton_Click(object? sender, EventArgs e)
    {
        Clipboard.SetText(_browserAddressText.Text);
        MessageForm.Show(
            this,
            "Address copied to clipboard.",
            "Information",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        );
    }

    private void HostnameText_TextChanged(object? sender, EventArgs e)
    {
        UpdateShareTab();
    }

    private void ShareVlcCheck_CheckedChanged(object? sender, EventArgs e)
    {
        UpdateShareTab();
    }

    private void ShareBrowserCheck_CheckedChanged(object? sender, EventArgs e)
    {
        UpdateShareTab();
    }

    private void UpdateShareTab()
    {
        if (_browserAddressText is not null)
            _browserAddressText.Text = $"http://{_hostnameText.Text}:777";

        foreach (var c in _shareBrowserControls)
            c.Enabled = _shareBrowserCheck.Checked;

        foreach (var c in _shareVlcControls)
            c.Enabled = _shareVlcCheck.Checked;
    }

    private void WikiLink_Click(object? sender, EventArgs e)
    {
        Process
            .Start(
                new ProcessStartInfo
                {
                    FileName = "https://github.com/brianluft/jackpot/wiki/Network-Sharing",
                    UseShellExecute = true,
                }
            )!
            .Dispose();
    }
}
