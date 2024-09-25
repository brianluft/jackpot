using IoPath = System.IO.Path;

namespace J.Core;

public sealed class ProcessTempDir : IDisposable
{
    public const string LOCK_FILENAME = "lock";

    private FileStream? _lockFile;
    private int _nextNumber = 1;

    public string Path { get; }

    public ProcessTempDir()
    {
        var appDir = IoPath.Combine(IoPath.GetTempPath(), "Jackpot");
        Directory.CreateDirectory(appDir);

        foreach (var dir in Directory.GetDirectories(appDir))
            Prune(dir);

        Path = IoPath.Combine(appDir, IoPath.GetRandomFileName());
        Directory.CreateDirectory(Path);
        _lockFile = new(IoPath.Combine(Path, LOCK_FILENAME), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
    }

    private static void Prune(string dir)
    {
        try
        {
            File.Delete(IoPath.Combine(dir, LOCK_FILENAME));
            Directory.Delete(dir, true);
        }
        catch
        {
            // It's still in use. Try again next time.
        }
    }

    public void Dispose()
    {
        _lockFile?.Dispose();
        _lockFile = null;
        Prune(Path);
    }

    public TempDir NewDir()
    {
        var number = Interlocked.Increment(ref _nextNumber);
        var path = IoPath.Combine(Path, number.ToString());
        Directory.CreateDirectory(path);
        return new(path);
    }
}
