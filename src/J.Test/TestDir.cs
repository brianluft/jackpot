namespace J.Test;

public static class TestDir
{
    public static string Path
    {
        get
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "J.Test");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static void Clear()
    {
        if (Directory.Exists(Path))
            Directory.Delete(Path, true);
        Directory.CreateDirectory(Path);
    }
}
