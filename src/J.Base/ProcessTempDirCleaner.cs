namespace J.Base;

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
                    File.Delete(Path.Combine(dir, Constants.TEMP_DIR_LOCK_FILENAME));
                    Directory.Delete(dir, true);
                }
                catch { }
            }
        }
        catch
        {
            // We did our best. Don't let it crash the app.
        }
    }
}
