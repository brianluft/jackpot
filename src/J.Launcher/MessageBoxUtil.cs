using System.Runtime.InteropServices;

namespace J.Launcher;

public static partial class MessageBoxUtil
{
    // Constants for MessageBox
    private const int MB_OK = 0x0;
    private const int MB_ICONERROR = 0x10;

    // P/Invoke declaration for MessageBox
    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    /// <summary>
    /// Shows an error message box with OK button
    /// </summary>
    /// <param name="message">The message to display</param>
    /// <returns>Return value from MessageBox (always 1 for OK button)</returns>
    public static int ShowError(string message)
    {
        return MessageBox(IntPtr.Zero, message, "Error", (uint)(MB_OK | MB_ICONERROR));
    }
}
