using System.Diagnostics;
using System.Runtime.InteropServices;
using J.Base;

namespace J.Launcher;

public static class Program
{
    public static int Main()
    {
        Process process;
        try
        {
            TaskbarUtil.SetTaskbarAppId();
            var dir = AppContext.BaseDirectory;

            // Are we on x64 or Arm64?
            var exeFilePath = RuntimeInformation.OSArchitecture switch
            {
                Architecture.Arm64 => Path.Combine(dir, "arm64", "Jackpot.App.exe"),
                Architecture.X64 => Path.Combine(dir, "x64", "Jackpot.App.exe"),
                _ => throw new PlatformNotSupportedException(),
            };

            ProcessStartInfo psi = new(exeFilePath) { UseShellExecute = false };

            process = Process.Start(psi)!;
        }
        catch (Exception ex)
        {
            MessageBoxUtil.ShowError("There was a problem starting Jackpot on your computer.\n\n" + ex.Message);
            return -1;
        }

        GC.Collect(
            GC.MaxGeneration, // Collect all generations
            GCCollectionMode.Forced, // Force immediate collection
            blocking: true, // Wait for collection to finish
            compacting: true // Compact the heap
        );

        using (process)
        {
            process.WaitForExit();
            Thread.Sleep(500);
            ProcessTempDirCleaner.Clean();
            return process.ExitCode;
        }
    }
}
