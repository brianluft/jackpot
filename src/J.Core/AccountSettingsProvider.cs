using System.ComponentModel;
using System.Diagnostics;
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
    private AccountSettings _current;

    public AccountSettingsProvider()
    {
        _current = Load();
    }

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
        AmazonS3Config config = new() { ServiceURL = _current.Endpoint };
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
                return new("https://", "", "", "", "", "", false, "", "");
            }
            var bytes = File.ReadAllBytes(filePath);
            var json = Decrypt(bytes);
            return JsonSerializer.Deserialize<AccountSettings>(json)!;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Failed to load account settings: " + ex.Message);
            return new("https://", "", "", "", "", "", false, "", "");
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
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Jackpot",
            "account-settings"
        );

        Console.Error.WriteLine("Account settings: " + path);
        return path;
    }

    public bool IsFfmpegInstalled()
    {
        // ffmpeg.exe must be on the $PATH. See if we can run ffmpeg --help.
        try
        {
            using var p = Process.Start(
                new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = "--help",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            )!;
            p.WaitForExit();
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    public bool IsVlcInstalled()
    {
        // Check that the registry key "Computer\HKEY_CLASSES_ROOT\Applications\vlc.exe" exists.
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(@"Applications\vlc.exe");
            return key is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
