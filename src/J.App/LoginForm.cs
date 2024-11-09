using System.Diagnostics;
using System.Text.Json;
using J.Core;
using J.Core.Data;

namespace J.App;

public sealed class LoginForm : Form
{
    private readonly AccountSettingsProvider _accountSettingsProvider;
    private readonly TableLayoutPanel _formTable,
        _accountTable,
        _bucketTable,
        _encryptionTable;
    private readonly TextBox _endpointText,
        _accessKeyIdText,
        _secretAccessKeyText,
        _bucketText,
        _passwordText,
        _m3u8FolderText,
        _m3u8HostnameText;
    private readonly Button _saveButton,
        _cancelButton,
        _copySettingsButton,
        _pasteSettingsButton,
        _deleteAllLocalDataButton;
    private readonly FlowLayoutPanel _saveCancelButtonsFlow,
        _m3u8Flow,
        _copyPasteButtonsFlow,
        _bucketHelpFlow,
        _keyHelpFlow;
    private readonly TabControl _tabControl;
    private readonly TabPage _accountPage,
        _bucketPage,
        _m3u8Page,
        _encryptionPage,
        _importExportPage;
    private readonly CheckBox _enableM3u8FolderCheck;
    private readonly LinkLabel _b2BucketLink,
        _b2KeyLink;
    private readonly PictureBox _b2BucketPicture,
        _b2BucketHelpPicture,
        _b2BucketIconPicture,
        _b2KeyPicture,
        _b2KeyHelpPicture,
        _b2KeyIconPicture,
        _encryptionIconPicture;
    private readonly Label _encryptionHelpLabel;

    public LoginForm(AccountSettingsProvider accountSettingsProvider)
    {
        _accountSettingsProvider = accountSettingsProvider;

        Ui ui = new(this);

        Controls.Add(_formTable = ui.NewTable(2, 2));
        {
            _formTable.Padding = ui.DefaultPadding;
            _formTable.Dock = DockStyle.Fill;

            _formTable.Controls.Add(_tabControl = ui.NewTabControl(), 0, 0);
            {
                _formTable.SetColumnSpan(_tabControl, 2);
                _formTable.RowStyles[0].SizeType = SizeType.Percent;
                _formTable.RowStyles[0].Height = 100;

                _tabControl.TabPages.Add(_bucketPage = ui.NewTabPage("B2 Bucket"));
                {
                    _bucketPage.Controls.Add(_bucketTable = ui.NewTable(2, 4));
                    {
                        _bucketTable.Padding = ui.DefaultPadding;

                        _bucketText = ui.AddPairToTable(_bucketTable, 1, 0, ui.NewLabeledTextBox("Bucket name:", 300));

                        _endpointText = ui.AddPairToTable(_bucketTable, 1, 1, ui.NewLabeledTextBox("Endpoint:", 300));

                        _bucketTable.Controls.Add(
                            _b2BucketIconPicture = ui.NewPictureBox(ui.GetScaledBitmapResource("Bucket.png", 32, 32)),
                            0,
                            0
                        );
                        {
                            _b2BucketIconPicture.Padding = ui.DefaultPadding;
                        }

                        _bucketTable.Controls.Add(_bucketHelpFlow = ui.NewFlowRow(), 0, 2);
                        {
                            _bucketTable.SetColumnSpan(_bucketHelpFlow, 2);
                            _bucketHelpFlow.Margin = ui.GetPadding(0, 32, 0, 0);

                            _bucketHelpFlow.Controls.Add(
                                _b2BucketHelpPicture = ui.NewPictureBox(ui.GetScaledBitmapResource("Help.png", 16, 16))
                            );
                            {
                                _b2BucketHelpPicture.Dock = DockStyle.Bottom;
                            }

                            _bucketHelpFlow.Controls.Add(ui.NewLabel("Create a bucket on the"));

                            _bucketHelpFlow.Controls.Add(_b2BucketLink = ui.NewLinkLabel("B2 Cloud Storage Buckets"));
                            {
                                _b2BucketLink.LinkClicked += B2BucketLink_LinkClicked;
                            }

                            _bucketHelpFlow.Controls.Add(ui.NewLabel("web page:"));
                        }

                        _bucketTable.Controls.Add(
                            _b2BucketPicture = ui.NewPictureBox(
                                ui.GetScaledBitmapResource("B2BucketHelp.png", 448, 191)
                            ),
                            0,
                            3
                        );
                        {
                            _bucketTable.SetColumnSpan(_b2BucketPicture, 2);
                            _b2BucketPicture.Margin = ui.TopSpacing;

                            _b2BucketPicture.BorderStyle = BorderStyle.FixedSingle;
                        }
                    }
                }

                _tabControl.TabPages.Add(_accountPage = ui.NewTabPage("B2 Application Key"));
                {
                    _accountPage.Controls.Add(_accountTable = ui.NewTable(2, 4));
                    {
                        _accountTable.Padding = ui.DefaultPadding;

                        _accountTable.Controls.Add(
                            _b2KeyIconPicture = ui.NewPictureBox(ui.GetScaledBitmapResource("Key.png", 32, 32)),
                            0,
                            0
                        );
                        {
                            _b2KeyIconPicture.Padding = ui.DefaultPadding;
                        }

                        _accessKeyIdText = ui.AddPairToTable(_accountTable, 1, 0, ui.NewLabeledTextBox("keyID:", 300));

                        _secretAccessKeyText = ui.AddPairToTable(
                            _accountTable,
                            1,
                            1,
                            ui.NewLabeledTextBox("applicationKey:", 300)
                        );
                        {
                            _secretAccessKeyText.PasswordChar = '•';
                        }

                        _accountTable.Controls.Add(_keyHelpFlow = ui.NewFlowRow(), 0, 2);
                        {
                            _accountTable.SetColumnSpan(_keyHelpFlow, 2);
                            _keyHelpFlow.Margin = ui.GetPadding(0, 32, 0, 0);

                            _keyHelpFlow.Controls.Add(
                                _b2KeyHelpPicture = ui.NewPictureBox(ui.GetScaledBitmapResource("Help.png", 16, 16))
                            );
                            {
                                _b2KeyHelpPicture.Dock = DockStyle.Bottom;
                            }

                            _keyHelpFlow.Controls.Add(ui.NewLabel("Create an application key on the"));

                            _keyHelpFlow.Controls.Add(_b2KeyLink = ui.NewLinkLabel("B2 Application Keys"));
                            {
                                _b2KeyLink.LinkClicked += B2KeyLink_LinkClicked;
                            }

                            _keyHelpFlow.Controls.Add(ui.NewLabel("web page:"));
                        }

                        _accountTable.Controls.Add(
                            _b2KeyPicture = ui.NewPictureBox(ui.GetScaledBitmapResource("B2KeyHelp.png", 500, 191)),
                            0,
                            3
                        );
                        {
                            _accountTable.SetColumnSpan(_b2KeyPicture, 2);
                            _b2KeyPicture.Margin = ui.TopSpacing;

                            _b2KeyPicture.BorderStyle = BorderStyle.FixedSingle;
                        }
                    }
                }

                _tabControl.TabPages.Add(_encryptionPage = ui.NewTabPage("Encryption"));
                {
                    _encryptionPage.Controls.Add(_encryptionTable = ui.NewTable(2, 2));
                    {
                        _encryptionTable.Padding = ui.DefaultPadding;

                        _encryptionTable.Controls.Add(
                            _encryptionIconPicture = ui.NewPictureBox(
                                ui.GetScaledBitmapResource("Encryption.png", 32, 32)
                            ),
                            0,
                            0
                        );
                        {
                            _encryptionIconPicture.Padding = ui.DefaultPadding;
                        }

                        _passwordText = ui.AddPairToTable(
                            _encryptionTable,
                            1,
                            0,
                            ui.NewLabeledTextBox("Encryption password:", 300)
                        );
                        _passwordText.Margin += ui.BottomSpacingBig;

                        _encryptionTable.Controls.Add(
                            _encryptionHelpLabel = ui.NewLabel(
                                "Your content is secured with end-to-end encryption.\n\n"
                                    + "When creating a new library, choose a new password.\n\n"
                                    + "To connect to an existing library, make sure to use the right password."
                            ),
                            1,
                            1
                        );
                        {
                            _encryptionTable.SetColumnSpan(_encryptionHelpLabel, 2);
                        }
                    }
                }

                _tabControl.TabPages.Add(_m3u8Page = ui.NewTabPage("M3U8 Sync"));
                {
                    _m3u8Page.Controls.Add(_m3u8Flow = ui.NewFlowColumn());
                    {
                        _m3u8Flow.Padding = ui.DefaultPadding;
                        Control p;

                        _m3u8Flow.Controls.Add(
                            ui.NewLabel(
                                "Non-Windows devices can stream videos through Jackpot using the VLC app.\n\nJackpot can maintain a folder of M3U8 files for them to access via Windows file sharing."
                            )
                        );

                        _m3u8Flow.Controls.Add(
                            _enableM3u8FolderCheck = ui.NewCheckBox("Store M3U8 files in a local folder")
                        );
                        {
                            _enableM3u8FolderCheck.Margin += ui.TopSpacingBig;
                        }

                        (p, _m3u8FolderText) = ui.NewLabeledOpenFolderTextBox("Folder:", 400, _ => { });
                        _m3u8Flow.Controls.Add(p);

                        (p, _m3u8HostnameText) = ui.NewLabeledTextBox("Host or IP address to use in M3U8 files:", 200);
                        _m3u8Flow.Controls.Add(p);
                    }
                }
            }

            _tabControl.TabPages.Add(_importExportPage = ui.NewTabPage("Import/Export Settings"));
            {
                _importExportPage.Controls.Add(_copyPasteButtonsFlow = ui.NewFlowColumn());
                {
                    _copyPasteButtonsFlow.Padding = ui.DefaultPadding;

                    _copyPasteButtonsFlow.Controls.Add(
                        ui.NewLabel(
                            "Use the buttons below to import or export a copy of your login information.\n\nSave this text in your password manager for safe keeping."
                        )
                    );

                    _copyPasteButtonsFlow.Controls.Add(_copySettingsButton = ui.NewButton("Copy JSON"));
                    {
                        _copySettingsButton.Margin += ui.TopSpacingBig;
                        _copySettingsButton.Click += CopySettingsButton_Click;
                    }

                    _copyPasteButtonsFlow.Controls.Add(_pasteSettingsButton = ui.NewButton("Paste JSON"));
                    {
                        _pasteSettingsButton.Margin += ui.TopSpacing + ui.GetPadding(0, 0, 0, 36);
                        _pasteSettingsButton.Click += PasteSettingsButton_Click;
                    }

                    _copyPasteButtonsFlow.Controls.Add(
                        ui.NewLabel(
                            "If you plan to uninstall Jackpot and not reinstall, use this button to remove Jackpot data\nfrom your computer."
                        )
                    );

                    _copyPasteButtonsFlow.Controls.Add(
                        _deleteAllLocalDataButton = ui.NewButton("Delete all local data")
                    );
                    {
                        _deleteAllLocalDataButton.Margin = ui.TopSpacingBig;
                        _deleteAllLocalDataButton.Click += DeleteAllLocalDataButton_Click;
                    }
                }
            }

            _formTable.Controls.Add(_saveCancelButtonsFlow = ui.NewFlowRow(), 1, 1);
            {
                _saveCancelButtonsFlow.Dock = DockStyle.Right;
                _saveCancelButtonsFlow.Margin = ui.TopSpacingBig;

                _saveCancelButtonsFlow.Controls.Add(_saveButton = ui.NewButton("Log in"));
                {
                    _saveButton.Click += SaveButton_Click;
                }

                _saveCancelButtonsFlow.Controls.Add(_cancelButton = ui.NewButton("Exit"));
                {
                    _cancelButton.Click += CancelButton_Click;
                }
            }
        }

        void Space(Control c)
        {
            if (c is Label l)
            {
                l.Margin = ui.GetPadding(0, 8, 0, 1);
            }
            foreach (Control cc in c.Controls)
                Space(cc);
        }
        Space(this);

        Text = "Jackpot Login";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = Size = ui.GetSize(560, 540);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        AcceptButton = _saveButton;
        CancelButton = _cancelButton;
        Icon = ui.GetIconResource("App.ico");
        ShowIcon = true;
        ShowInTaskbar = true;

        Load += delegate
        {
            SetAccountSettings(accountSettingsProvider.Current);
        };
    }

    private void B2KeyLink_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        ProcessStartInfo psi = new("https://secure.backblaze.com/app_keys.htm") { UseShellExecute = true };
        Process.Start(psi)!.Dispose();
    }

    private void B2BucketLink_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        ProcessStartInfo psi = new("https://secure.backblaze.com/b2_buckets.htm") { UseShellExecute = true };
        Process.Start(psi)!.Dispose();
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        try
        {
            _accountSettingsProvider.Current = GetAccountSettings();
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private AccountSettings GetAccountSettings()
    {
        if (string.IsNullOrWhiteSpace(_endpointText.Text))
            throw new Exception("Please enter an endpoint.");

        if (string.IsNullOrWhiteSpace(_accessKeyIdText.Text))
            throw new Exception("Please enter a keyID.");

        if (string.IsNullOrWhiteSpace(_secretAccessKeyText.Text))
            throw new Exception("Please enter an applicationKey.");

        if (string.IsNullOrWhiteSpace(_bucketText.Text))
            throw new Exception("Please enter a bucket name.");

        if (string.IsNullOrWhiteSpace(_passwordText.Text))
            throw new Exception("Please enter an encryption password.");

        if (_enableM3u8FolderCheck.Checked)
        {
            if (string.IsNullOrWhiteSpace(_m3u8FolderText.Text))
                throw new Exception("Please enter a folder for .M3U8 files.");
        }

        return new(
            _endpointText.Text,
            _accessKeyIdText.Text,
            _secretAccessKeyText.Text,
            _bucketText.Text,
            _passwordText.Text,
            _enableM3u8FolderCheck.Checked,
            _m3u8FolderText.Text,
            _m3u8HostnameText.Text
        );
    }

    private void SetAccountSettings(AccountSettings settings)
    {
        _endpointText.Text = settings.Endpoint;
        _accessKeyIdText.Text = settings.AccessKeyId;
        _secretAccessKeyText.Text = settings.SecretAccessKey;
        _bucketText.Text = settings.Bucket;
        _passwordText.Text = settings.PasswordText;
        _enableM3u8FolderCheck.Checked = settings.EnableLocalM3u8Folder;
        _m3u8FolderText.Text = settings.LocalM3u8FolderPath;
        _m3u8HostnameText.Text = settings.M3u8Hostname;
    }

    private void CopySettingsButton_Click(object? sender, EventArgs e)
    {
        var settings = GetAccountSettings();
        var json = JsonSerializer.Serialize(settings);
        Clipboard.SetText(json);
        MessageBox.Show(
            this,
            "Account settings have been copied to the clipboard in JSON format.",
            "Account Settings",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        );
    }

    private void PasteSettingsButton_Click(object? sender, EventArgs e)
    {
        try
        {
            var json = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(json))
                throw new Exception("There is nothing in the clipboard.");
            var settings = JsonSerializer.Deserialize<AccountSettings>(json);
            SetAccountSettings(settings);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "There was a problem importing account settings from the clipboard.\n\n" + ex.Message,
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private void CancelButton_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void DeleteAllLocalDataButton_Click(object? sender, EventArgs e)
    {
        // Are you sure?
        var response = MessageBox.Show(
            this,
            "Are you sure you want to delete all local Jackpot data?\n\nThis will remove all settings and cached data from your computer.",
            "Delete All Local Data",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning
        );
        if (response != DialogResult.OK)
            return;

        try
        {
            SimpleProgressForm.Do(
                this,
                "Deleting local data...",
                (updateProgress, cancel) =>
                {
                    Delete(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Jackpot")
                    );
                    Delete(
                        Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "Jackpot"
                        )
                    );
                }
            );

            MessageBox.Show(
                this,
                "All local data has been deleted.",
                "Delete All Local Data",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "There was a problem deleting the local data.\n\n" + ex.Message,
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }

        static void Delete(string dir)
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }
}
