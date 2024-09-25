namespace J.Core.Data;

public readonly record struct AccountSettings(
    string Endpoint,
    string AccessKeyId,
    string SecretAccessKey,
    string Bucket,
    string DatabaseFilePath,
    string PasswordText,
    bool EnableLocalM3u8Folder,
    string LocalM3u8FolderPath,
    string M3u8Hostname
)
{
    public Password Password => new(PasswordText);
}
