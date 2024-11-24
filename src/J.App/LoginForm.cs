using System.Diagnostics;
using System.Text.Json;
using J.Core;
using J.Core.Data;

namespace J.App;

public sealed class LoginForm : Form
{
    private readonly AccountSettingsProvider _accountSettingsProvider;
    private readonly TableLayoutPanel _formTable,
        _b2Table;
    private readonly TextBox _endpointText,
        _accessKeyIdText,
        _secretAccessKeyText,
        _bucketText,
        _passwordText;
    private readonly Button _saveButton,
        _cancelButton,
        _toolsButton;
    private readonly FlowLayoutPanel _saveCancelButtonsFlow,
        _toolsButtonFlow,
        _bucketHelpFlow,
        _keyHelpFlow,
        _bucketFlow,
        _keyFlow,
        _encryptionFlow;
    private readonly LinkLabel _b2BucketLink,
        _b2KeyLink;
    private readonly PictureBox _b2BucketIconPicture,
        _b2KeyIconPicture,
        _encryptionIconPicture;
    private readonly Label _encryptionLabel;
    private readonly ContextMenuStrip _toolsMenu;
    private readonly ToolStripMenuItem _copyJsonItem,
        _pasteJsonItem;

    public LoginForm(AccountSettingsProvider accountSettingsProvider)
    {
        _accountSettingsProvider = accountSettingsProvider;

        Ui ui = new(this);
        Label label;
        Control p;

        Controls.Add(_formTable = ui.NewTable(2, 2));
        {
            _formTable.Padding = ui.DefaultPadding;
            _formTable.Dock = DockStyle.Fill;

            _formTable.Controls.Add(_b2Table = ui.NewTable(2, 3), 0, 0);
            {
                _formTable.SetColumnSpan(_b2Table, 2);
                _b2Table.Padding = ui.LeftSpacing + ui.RightSpacing;

                _b2Table.Controls.Add(
                    _b2BucketIconPicture = ui.NewPictureBox(
                        ui.InvertColorsInPlace(ui.GetScaledBitmapResource("Bucket.png", 32, 32))
                    ),
                    0,
                    0
                );
                {
                    _b2BucketIconPicture.Padding = ui.RightSpacing;
                }

                _b2Table.Controls.Add(_bucketFlow = ui.NewFlowColumn(), 1, 0);
                {
                    _bucketFlow.Padding += ui.BottomSpacingBig;

                    _bucketFlow.Controls.Add(_bucketHelpFlow = ui.NewFlowRow());
                    {
                        _bucketHelpFlow.Margin += ui.BottomSpacing;

                        _bucketHelpFlow.Controls.Add(label = ui.NewLabel("Create a bucket at:"));
                        {
                            label.Margin = label.Margin with { Right = 0 };
                            label.Padding = label.Padding with { Right = 0 };
                            label.Dock = DockStyle.Fill;
                            label.TextAlign = ContentAlignment.MiddleCenter;
                        }

                        _bucketHelpFlow.Controls.Add(_b2BucketLink = ui.NewLinkLabel("B2 Cloud Storage Buckets"));
                        {
                            _b2BucketLink.Margin = label.Margin with { Left = 0, Right = 0 };
                            _b2BucketLink.Padding = label.Padding with { Left = 0, Right = 0 };
                            _b2BucketLink.LinkClicked += B2BucketLink_LinkClicked;
                            _b2BucketLink.Dock = DockStyle.Fill;
                            _b2BucketLink.TextAlign = ContentAlignment.MiddleCenter;
                        }
                    }

                    _bucketFlow.Controls.Add(ui.NewLabeledPair("Bucket name:", _bucketText = ui.NewTextBox(400)));
                    {
                        _bucketText.Margin += ui.BottomSpacing;
                    }

                    _bucketFlow.Controls.Add(ui.NewLabeledPair("Endpoint:", _endpointText = ui.NewTextBox(400)));
                    {
                        _endpointText.Margin += ui.BottomSpacing;
                    }
                }

                _b2Table.Controls.Add(
                    _b2KeyIconPicture = ui.NewPictureBox(
                        ui.InvertColorsInPlace(ui.GetScaledBitmapResource("Key.png", 32, 32))
                    ),
                    0,
                    1
                );
                {
                    _b2KeyIconPicture.Padding = ui.RightSpacing;
                }

                _b2Table.Controls.Add(_keyFlow = ui.NewFlowColumn(), 1, 1);
                {
                    _keyFlow.Padding += ui.BottomSpacingBig;

                    _keyFlow.Controls.Add(_keyHelpFlow = ui.NewFlowRow());
                    {
                        _keyHelpFlow.Margin += ui.BottomSpacing;

                        _keyHelpFlow.Controls.Add(label = ui.NewLabel("Create an application key at:"));
                        {
                            label.Margin = label.Margin with { Right = 0 };
                            label.Padding = label.Padding with { Right = 0 };
                            label.Dock = DockStyle.Fill;
                            label.TextAlign = ContentAlignment.MiddleCenter;
                        }

                        _keyHelpFlow.Controls.Add(_b2KeyLink = ui.NewLinkLabel("B2 Application Keys"));
                        {
                            _b2KeyLink.LinkClicked += B2KeyLink_LinkClicked;
                            _b2KeyLink.Margin = label.Margin with { Left = 0, Right = 0 };
                            _b2KeyLink.Padding = label.Padding with { Left = 0, Right = 0 };
                            _b2KeyLink.Dock = DockStyle.Fill;
                            _b2KeyLink.TextAlign = ContentAlignment.MiddleCenter;
                        }
                    }

                    _keyFlow.Controls.Add(ui.NewLabeledPair("keyID:", _accessKeyIdText = ui.NewTextBox(400)));
                    {
                        _accessKeyIdText.Margin += ui.BottomSpacing;
                    }

                    _keyFlow.Controls.Add(
                        ui.NewLabeledPair("applicationKey:", _secretAccessKeyText = ui.NewTextBox(400))
                    );
                    {
                        _secretAccessKeyText.Margin += ui.BottomSpacing;
                        _secretAccessKeyText.PasswordChar = '•';
                    }
                }

                _b2Table.Controls.Add(
                    _encryptionIconPicture = ui.NewPictureBox(
                        ui.InvertColorsInPlace(ui.GetScaledBitmapResource("Encryption.png", 32, 32))
                    ),
                    0,
                    2
                );
                {
                    _encryptionIconPicture.Padding = ui.RightSpacing;
                }

                _b2Table.Controls.Add(_encryptionFlow = ui.NewFlowColumn(), 1, 2);
                {
                    _encryptionFlow.Controls.Add(
                        _encryptionLabel = ui.NewLabel("Choose a password to encrypt your library files.")
                    );
                    {
                        _encryptionLabel.Margin = ui.BottomSpacing;
                    }

                    _encryptionFlow.Controls.Add(ui.NewLabeledPair("Password:", _passwordText = ui.NewTextBox(400)));
                    {
                        _passwordText.Margin += ui.BottomSpacing;
                        _passwordText.PasswordChar = '•';
                    }
                }
            }

            _formTable.Controls.Add(_toolsButtonFlow = ui.NewFlowRow(), 0, 1);
            {
                _toolsButtonFlow.Margin = ui.TopSpacingBig;
                _toolsButtonFlow.Controls.Add(_toolsButton = ui.NewButton("Tools 🞃"));
                {
                    _toolsButton.Click += ToolsButton_Click;
                }
            }

            _formTable.Controls.Add(_saveCancelButtonsFlow = ui.NewFlowRow(), 1, 1);
            {
                _saveCancelButtonsFlow.Dock = DockStyle.Right;
                _saveCancelButtonsFlow.Margin = ui.TopSpacingBig;

                _saveCancelButtonsFlow.Controls.Add(_saveButton = ui.NewButton("Connect"));
                {
                    _saveButton.Click += SaveButton_Click;
                    _saveButton.Margin += ui.ButtonSpacing;
                }

                _saveCancelButtonsFlow.Controls.Add(_cancelButton = ui.NewButton("Exit"));
                {
                    _cancelButton.Click += CancelButton_Click;
                }
            }
        }

        _toolsMenu = ui.NewContextMenuStrip();
        {
            _toolsMenu.Items.Add(_copyJsonItem = ui.NewToolStripMenuItem("Copy as JSON"));
            _copyJsonItem.Click += CopySettingsButton_Click;

            _toolsMenu.Items.Add(_pasteJsonItem = ui.NewToolStripMenuItem("Paste JSON"));
            _pasteJsonItem.Click += PasteSettingsButton_Click;
        }

        Text = "Jackpot Media Library";
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        AcceptButton = _saveButton;
        CancelButton = _cancelButton;
        Icon = ui.GetIconResource("App.ico");
        ShowIcon = true;
        ShowInTaskbar = true;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        SetAccountSettings(_accountSettingsProvider.Current);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _saveButton.Focus();
    }

    private void B2KeyLink_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        ProcessStartInfo psi =
            new("msedge.exe", "\"https://secure.backblaze.com/app_keys.htm\"") { UseShellExecute = true };
        Process.Start(psi)!.Dispose();
    }

    private void B2BucketLink_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        ProcessStartInfo psi =
            new("msedge.exe", "\"https://secure.backblaze.com/b2_buckets.htm\"") { UseShellExecute = true };
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

        return new(
            _endpointText.Text,
            _accessKeyIdText.Text,
            _secretAccessKeyText.Text,
            _bucketText.Text,
            _passwordText.Text
        );
    }

    private void SetAccountSettings(AccountSettings settings)
    {
        _endpointText.Text = settings.Endpoint;
        _accessKeyIdText.Text = settings.AccessKeyId;
        _secretAccessKeyText.Text = settings.SecretAccessKey;
        _bucketText.Text = settings.Bucket;
        _passwordText.Text = settings.PasswordText;
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

    private void ToolsButton_Click(object? sender, EventArgs e)
    {
        _toolsMenu.Show(_toolsButton, 0, _toolsButton.Height);
    }
}
