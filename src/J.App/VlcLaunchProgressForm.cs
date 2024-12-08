using System.Diagnostics;
using System.Runtime.InteropServices;
using J.Base;
using J.Core;

namespace J.App;

public sealed partial class VlcLaunchProgressForm : Form
{
    private readonly FlowLayoutPanel _flow;
    private readonly Label _label;
    private readonly ProgressBar _progressBar;
    private readonly ProcessStartInfo _psi;

    public VlcLaunchProgressForm(ProcessStartInfo psi)
    {
        _psi = psi;
        Ui ui = new(this);

        Controls.Add(_flow = ui.NewFlowColumn());
        {
            _flow.Padding = ui.DefaultPadding;

            _flow.Controls.Add(_label = ui.NewLabel("Opening movie..."));
            {
                _label.Margin += ui.BottomSpacingBig;
            }

            _flow.Controls.Add(_progressBar = ui.NewProgressBar(300));
            {
                _progressBar.Style = ProgressBarStyle.Marquee;
                _progressBar.MarqueeAnimationSpeed = 10;
            }
        }

        Text = "VLC";
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        FormBorderStyle = FormBorderStyle.Fixed3D;
        ControlBox = false;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);

        try
        {
            await Task.Run(() =>
                {
                    using var p = Process.Start(_psi)!;
                    ApplicationSubProcesses.Add(p);
                    PowerThrottlingUtil.DisablePowerThrottling(p);
                    WaitForWindow(p, TimeSpan.FromSeconds(10));
                })
                .ConfigureAwait(true);

            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Unable to launch VLC.\n\n" + ex.Message,
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
            Close();
        }
    }

    private static void WaitForWindow(Process process, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(process);

        if (process.HasExited)
            return;

        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < timeout)
        {
            if (HasVisibleWindow(process))
                return;

            Thread.Sleep(100);
        }
    }

    private static bool HasVisibleWindow(Process process)
    {
        try
        {
            // Refresh the process info to get the current threads
            process.Refresh();

            // Check each thread in the process
            foreach (ProcessThread thread in process.Threads)
            {
                bool found = false;

                // Enumerate windows for this thread
                NativeMethods.EnumThreadWindows(
                    (uint)thread.Id,
                    (hWnd, lParam) =>
                    {
                        if (NativeMethods.IsWindowVisible(hWnd))
                        {
                            found = true;
                            return false; // Stop enumeration
                        }
                        return true; // Continue enumeration
                    },
                    IntPtr.Zero
                );

                if (found)
                    return true;
            }
        }
        catch (InvalidOperationException)
        {
            // Process has exited or access was denied
            return false;
        }
        catch (Exception)
        {
            // Handle any other unexpected errors gracefully
            return false;
        }

        return false;
    }

    private static partial class NativeMethods
    {
        public delegate bool EnumThreadWindowsProc(IntPtr hWnd, IntPtr lParam);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool EnumThreadWindows(uint threadId, EnumThreadWindowsProc enumFunc, IntPtr lParam);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool IsWindowVisible(IntPtr hWnd);
    }
}
