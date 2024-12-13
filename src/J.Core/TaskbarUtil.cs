using System.Runtime.InteropServices;

namespace J.Core;

public static partial class TaskbarUtil
{
    private const string TASKBAR_APP_ID = "BrianLuft.Jackpot";

    public static void SetTaskbarAppId()
    {
        try
        {
            NativeMethods.SetCurrentProcessExplicitAppUserModelID(TASKBAR_APP_ID);
        }
        catch
        {
            // Not essential.
        }
    }

    private static partial class NativeMethods
    {
        [LibraryImport("shell32.dll")]
        public static partial void SetCurrentProcessExplicitAppUserModelID(
            [MarshalAs(UnmanagedType.LPWStr)] string AppID
        );
    }
}
