namespace J.Core;

public sealed class TempDir(string path) : IDisposable
{
    public string Path => path;

    public void Dispose()
    {
        try
        {
            Directory.Delete(path, true);
        }
        catch
        {
            // The prune process will get it sometime in the future.
        }
    }
}
