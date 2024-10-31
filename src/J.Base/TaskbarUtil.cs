using System.Runtime.InteropServices;

namespace J.Base;

public static partial class TaskbarUtil
{
    public static void SetTaskbarAppId()
    {
        try
        {
            NativeMethods.SetCurrentProcessExplicitAppUserModelID(Constants.TASKBAR_APP_ID);
        }
        catch
        {
            // Not essential.
        }
    }

    private static partial class NativeMethods
    {
        [LibraryImport("shell32.dll", SetLastError = true)]
        public static partial void SetCurrentProcessExplicitAppUserModelID(
            [MarshalAs(UnmanagedType.LPWStr)] string AppID
        );
    }
}
