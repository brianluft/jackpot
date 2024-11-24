namespace J.Core.Data;

public readonly record struct M3u8SyncSettings(
    bool EnableLocalM3u8Folder,
    string LocalM3u8FolderPath,
    string M3u8Hostname
)
{
    public static M3u8SyncSettings Default => new(false, "", "localhost");
}
