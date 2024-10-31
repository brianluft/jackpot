using J.Base;
using IoPath = System.IO.Path;

namespace J.Core;

public sealed class ProcessTempDir : IDisposable
{
    private FileStream? _lockFile;
    private int _nextNumber = 1;

    public string Path { get; }

    public ProcessTempDir()
    {
        var appDir = IoPath.Combine(IoPath.GetTempPath(), "Jackpot");
        Directory.CreateDirectory(appDir);

        ProcessTempDirCleaner.Clean();

        Path = IoPath.Combine(appDir, IoPath.GetRandomFileName());
        Directory.CreateDirectory(Path);
        _lockFile = new(
            IoPath.Combine(Path, Constants.TEMP_DIR_LOCK_FILENAME),
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None
        );
    }

    public void Dispose()
    {
        _lockFile?.Dispose();
        _lockFile = null;

        ProcessTempDirCleaner.Clean();
    }

    public TempDir NewDir()
    {
        var number = Interlocked.Increment(ref _nextNumber);
        var path = IoPath.Combine(Path, number.ToString());
        Directory.CreateDirectory(path);
        return new(path);
    }
}
