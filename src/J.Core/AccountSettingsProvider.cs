using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amazon.S3;
using J.Core.Data;
using Microsoft.Win32;

namespace J.Core;

public sealed class AccountSettingsProvider
{
    private readonly object _lock = new();
    private AccountSettings _current = Load();

    public event EventHandler? SettingsChanged;

    public AccountSettings Current
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
        set
        {
            lock (_lock)
            {
                _current = value;
                Save();
            }

            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public IAmazonS3 CreateAmazonS3Client()
    {
        AmazonS3Config config =
            new()
            {
                ServiceURL = _current.Endpoint,
                Timeout = TimeSpan.FromSeconds(30),
                MaxErrorRetry = 3,
            };
        return new AmazonS3Client(_current.AccessKeyId, _current.SecretAccessKey, config);
    }

    private void Save()
    {
        var filePath = GetFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var json = JsonSerializer.Serialize(_current);
        var bytes = Encrypt(json);
        File.WriteAllBytes(filePath, bytes);
    }

    private static AccountSettings Load()
    {
        try
        {
            var filePath = GetFilePath();
            if (!File.Exists(filePath))
            {
                Console.Error.WriteLine("Account settings file doesn't exist: " + filePath);
                return AccountSettings.Empty;
            }
            var bytes = File.ReadAllBytes(filePath);
            var json = Decrypt(bytes);
            var settings = JsonSerializer.Deserialize<AccountSettings>(json)!;
            return settings.Upgrade();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Failed to load account settings: " + ex.Message);
            return AccountSettings.Empty;
        }
    }

    private static byte[] Encrypt(string input)
    {
        var rawBytes = Encoding.UTF8.GetBytes(input);
        return ProtectedData.Protect(rawBytes, null, DataProtectionScope.CurrentUser);
    }

    private static string Decrypt(byte[] input)
    {
        var rawBytes = ProtectedData.Unprotect(input, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(rawBytes);
    }

    private static string GetFilePath()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Jackpot",
            "account-settings"
        );

        Console.Error.WriteLine("Account settings: " + path);
        return path;
    }
}
