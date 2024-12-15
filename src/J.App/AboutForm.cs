using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using J.Core;

namespace J.App;

public sealed class AboutForm : Form
{
    private readonly ProcessTempDir _processTempDir;
    private readonly Client _client;
    private readonly TableLayoutPanel _table;
    private readonly FlowLayoutPanel _flow1,
        _flow2;
    private readonly Button _closeButton;

    public AboutForm(ProcessTempDir processTempDir, Client client)
    {
        _processTempDir = processTempDir;
        _client = client;
        Ui ui = new(this);
        Control ctl;

        var assembly = typeof(MainForm).Assembly;
        var version = assembly.GetName().Version;

        Controls.Add(_table = ui.NewTable(2, 2));
        {
            _table.Controls.Add(ctl = ui.NewPictureBox(ui.GetScaledBitmapResource("App.png", 48, 48)), 0, 0);
            {
                ctl.Margin += ui.RightSpacing;
            }

            _table.Controls.Add(_flow1 = ui.NewFlowColumn(), 1, 0);
            {
                _flow1.Margin += ui.BottomSpacingBig;

                _flow1.Controls.Add(ctl = ui.NewLabel("Jackpot Media Library"));
                {
                    ctl.Font = ui.BigBoldFont;
                    ctl.Margin += ui.GetPadding(0, 0, 30, 0);
                    ;
                }

                _flow1.Controls.Add(ctl = ui.NewLabel($"Version {version}"));
                {
                    ctl.Margin += ui.BottomSpacingBig;
                }

                _flow1.Controls.Add(ctl = ui.NewLinkLabel("Jackpot website"));
                {
                    ctl.Margin += ui.BottomSpacing;
                    ctl.Click += WebsiteLink_Click;
                }

                _flow1.Controls.Add(ctl = ui.NewLinkLabel("License info"));
                {
                    ctl.Margin += ui.BottomSpacing;
                    ctl.Click += LicenseInfoLink_Click;
                }

                _flow1.Controls.Add(ctl = ui.NewLinkLabel("Diagnostic log"));
                {
                    ctl.Margin += ui.BottomSpacing;
                    ctl.Click += ViewDiagnosticLogLink_Click;
                }
            }

            _table.Controls.Add(_flow2 = ui.NewFlowColumn(), 1, 1);
            {
                _flow2.Dock = DockStyle.Right;

                _flow2.Controls.Add(_closeButton = ui.NewButton("Close"));
                {
                    _closeButton.Click += CloseButton_Click;
                }
            }
        }

        Padding = ui.DefaultPadding;
        Text = "About Jackpot";
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        AcceptButton = _closeButton;
        CancelButton = _closeButton;
    }

    private void WebsiteLink_Click(object? sender, EventArgs e)
    {
        Process
            .Start(new ProcessStartInfo { FileName = "https://jackpotmedialibrary.com", UseShellExecute = true })!
            .Dispose();
    }

    private void LicenseInfoLink_Click(object? sender, EventArgs e)
    {
        Process
            .Start(
                new ProcessStartInfo(
                    "msedge.exe",
                    "\"" + Path.Combine(AppContext.BaseDirectory, "Resources", "License.html") + "\""
                )
                {
                    UseShellExecute = true,
                }
            )!
            .Dispose();
    }

    private void ViewDiagnosticLogLink_Click(object? sender, EventArgs e)
    {
        var filePath = Path.Combine(_processTempDir.Path, "server.log");
        File.WriteAllLines(filePath, _client.GetLog());
        Process.Start("notepad.exe", filePath)!.Dispose();
    }

    private void CloseButton_Click(object? sender, EventArgs e)
    {
        Close();
    }
}
