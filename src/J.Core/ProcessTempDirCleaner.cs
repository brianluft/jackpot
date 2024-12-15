namespace J.Core;

public static class ProcessTempDirCleaner
{
    public static void Clean()
    {
        try
        {
            var appDir = Path.Combine(Path.GetTempPath(), "Jackpot");
            Directory.CreateDirectory(appDir);

            foreach (var dir in Directory.GetDirectories(appDir))
            {
                try
                {
                    File.Delete(Path.Combine(dir, ProcessTempDir.LOCK_FILENAME));
                    DeleteDirectory(dir);
                }
                catch { }
            }
        }
        catch
        {
            // We did our best. Don't let it crash the app.
        }
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            foreach (var file in Directory.GetFiles(path))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                catch { }

                try
                {
                    File.Delete(file);
                }
                catch { }
            }
        }
        catch { }

        try
        {
            foreach (var dir in Directory.GetDirectories(path))
            {
                DeleteDirectory(dir);
            }
        }
        catch { }

        try
        {
            Directory.Delete(path, true);
        }
        catch { }
    }
}
