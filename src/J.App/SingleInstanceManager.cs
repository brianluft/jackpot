using System.Runtime.InteropServices;

namespace J.App;

public sealed partial class SingleInstanceManager : IDisposable
{
    private const string MUTEX_NAME = "18DD90A2-E879-47C5-AE7C-40716A697999";
    private const string ACTIVATE_MESSAGE_NAME = "FA5E655A-4F8A-4EF6-A305-E0CB444941E4";

    private readonly Mutex _mutex;
    private readonly bool _hasHandle;
    private bool _disposed;

    public int ActivateMessageId { get; }
    public bool IsFirstInstance => _hasHandle;

    public SingleInstanceManager()
    {
        ActivateMessageId = NativeMethods.RegisterWindowMessageW(ACTIVATE_MESSAGE_NAME);
        _mutex = new(true, MUTEX_NAME, out _hasHandle);
    }

    public void ActivateFirstInstance()
    {
        NativeMethods.PostMessageW(NativeMethods.HWND_BROADCAST, ActivateMessageId, IntPtr.Zero, IntPtr.Zero);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            if (_hasHandle)
                _mutex.ReleaseMutex();
            _mutex.Dispose();
        }

        _disposed = true;
    }

    private static partial class NativeMethods
    {
        public const int HWND_BROADCAST = 0xffff;

        [LibraryImport("user32", StringMarshalling = StringMarshalling.Utf16)]
        public static partial int RegisterWindowMessageW(string message);

        [LibraryImport("user32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool PostMessageW(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);
    }
}
