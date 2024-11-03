using System.Text.Json;
using J.Core;
using J.Core.Data;

namespace J.App;

public sealed class LoginForm : Form
{
    private readonly AccountSettingsProvider _accountSettingsProvider;
    private readonly TableLayoutPanel _formTable,
        _cloudTable;
    private readonly TextBox _endpointText,
        _accessKeyIdText,
        _secretAccessKeyText,
        _bucketText,
        _databaseFilePathText,
        _passwordText,
        _m3u8FolderText,
        _m3u8HostnameText;
    private readonly Button _createDatabaseButton,
        _saveButton,
        _cancelButton,
        _copySettingsButton,
        _pasteSettingsButton;
    private readonly FlowLayoutPanel _saveCancelButtonsFlow,
        _localFlow,
        _copyPasteButtonsFlow;
    private readonly TabControl _tabControl;
    private readonly TabPage _cloudPage,
        _localPage;
    private readonly CheckBox _enableM3u8FolderCheck;

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

                _tabControl.TabPages.Add(_cloudPage = ui.NewTabPage("Cloud"));
                {
                    _cloudPage.Controls.Add(_cloudTable = ui.NewTable(1, 9));
                    {
                        _cloudTable.Padding = ui.DefaultPadding;

                        _endpointText = ui.AddPairToTable(
                            _cloudTable,
                            0,
                            0,
                            ui.NewLabeledTextBox("Endpoint URL:", 400)
                        );

                        _accessKeyIdText = ui.AddPairToTable(
                            _cloudTable,
                            0,
                            1,
                            ui.NewLabeledTextBox("Access key ID:", 400)
                        );

                        _secretAccessKeyText = ui.AddPairToTable(
                            _cloudTable,
                            0,
                            2,
                            ui.NewLabeledTextBox("Secret access key:", 400)
                        );
                        {
                            _secretAccessKeyText.PasswordChar = '•';
                        }

                        _bucketText = ui.AddPairToTable(_cloudTable, 0, 3, ui.NewLabeledTextBox("Bucket:", 400));

                        _passwordText = ui.AddPairToTable(
                            _cloudTable,
                            0,
                            4,
                            ui.NewLabeledTextBox("Choose an encryption password:", 400)
                        );
                    }
                }

                _tabControl.TabPages.Add(_localPage = ui.NewTabPage("Local"));
                {
                    _localPage.Controls.Add(_localFlow = ui.NewFlowColumn());
                    {
                        _localFlow.Padding = ui.DefaultPadding;
                        Control p;

                        (p, _databaseFilePathText) = ui.NewLabeledOpenFileTextBox(
                            "Database file:",
                            400,
                            ConfigureOpenFileDialog
                        );
                        _localFlow.Controls.Add(p);

                        _localFlow.Controls.Add(_createDatabaseButton = ui.NewButton("Create database..."));
                        {
                            _createDatabaseButton.Margin = ui.TopSpacingBig;
                            _createDatabaseButton.Click += CreateDatabaseButton_Click;
                        }

                        _localFlow.Controls.Add(
                            _enableM3u8FolderCheck = ui.NewCheckBox("Store .m3u8 files in a local folder")
                        );
                        {
                            _enableM3u8FolderCheck.Margin = ui.TopSpacingBig;
                        }

                        (p, _m3u8FolderText) = ui.NewLabeledOpenFolderTextBox(".m3u8 folder:", 400, _ => { });
                        _localFlow.Controls.Add(p);

                        (p, _m3u8HostnameText) = ui.NewLabeledTextBox("Host or IP address to use in .m3u8 files:", 200);
                        _localFlow.Controls.Add(p);
                    }
                }
            }

            _formTable.Controls.Add(_copyPasteButtonsFlow = ui.NewFlowRow(), 0, 1);
            {
                _copyPasteButtonsFlow.Margin = ui.TopSpacingBig;

                _copyPasteButtonsFlow.Controls.Add(_copySettingsButton = ui.NewButton("Copy"));
                {
                    _copySettingsButton.Click += CopySettingsButton_Click;
                }

                _copyPasteButtonsFlow.Controls.Add(_pasteSettingsButton = ui.NewButton("Paste"));
                {
                    _pasteSettingsButton.Click += PasteSettingsButton_Click;
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
        MinimumSize = Size = ui.GetSize(460, 450);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        AcceptButton = _saveButton;
        CancelButton = _cancelButton;
        Icon = ui.GetIconResource("App.ico");
        ShowIcon = true;
        ShowInTaskbar = false;

        Load += delegate
        {
            SetAccountSettings(accountSettingsProvider.Current);
        };
    }

    private void CreateDatabaseButton_Click(object? sender, EventArgs e)
    {
        using SaveFileDialog d =
            new()
            {
                AddToRecent = false,
                AutoUpgradeEnabled = true,
                CheckFileExists = false,
                CheckPathExists = false,
                DefaultExt = "key",
                Filter = "Database files (*.db)|*.db",
                RestoreDirectory = true,
                ShowHelp = false,
                SupportMultiDottedExtensions = true,
                Title = "Save new database file",
            };
        if (d.ShowDialog(this) != DialogResult.OK)
            return;

        LibraryProvider.Create(d.FileName);
        _databaseFilePathText.Text = d.FileName;
    }

    private void ConfigureOpenFileDialog(System.Windows.Forms.OpenFileDialog dialog)
    {
        dialog.AddToRecent = false;
        dialog.AutoUpgradeEnabled = true;
        dialog.CheckFileExists = true;
        dialog.CheckPathExists = true;
        dialog.Filter = "Key files (*.key)|*.key";
        dialog.Multiselect = false;
        dialog.RestoreDirectory = true;
        dialog.SelectReadOnly = true;
        dialog.ShowHelp = false;
        dialog.ShowReadOnly = false;
        dialog.Title = "Select key file";
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

    private AccountSettings GetAccountSettings() =>
        new(
            _endpointText.Text,
            _accessKeyIdText.Text,
            _secretAccessKeyText.Text,
            _bucketText.Text,
            _databaseFilePathText.Text,
            _passwordText.Text,
            _enableM3u8FolderCheck.Checked,
            _m3u8FolderText.Text,
            _m3u8HostnameText.Text
        );

    private void SetAccountSettings(AccountSettings settings)
    {
        _endpointText.Text = settings.Endpoint;
        _accessKeyIdText.Text = settings.AccessKeyId;
        _secretAccessKeyText.Text = settings.SecretAccessKey;
        _bucketText.Text = settings.Bucket;
        _databaseFilePathText.Text = settings.DatabaseFilePath;
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
}
