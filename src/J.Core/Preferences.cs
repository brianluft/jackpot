using System.Collections.Frozen;
using J.Core.Data;
using Microsoft.Data.Sqlite;

namespace J.Core;

public sealed class Preferences : IDisposable
{
    private readonly object _lock = new();
    private readonly FrozenDictionary<Key, object> _defaults;
    private SqliteConnection? _connection;

    public enum Key
    {
        Shared_UseShuffle,
        Shared_VlcInstallationToUse,
        ConvertMoviesForm_VideoQuality,
        ConvertMoviesForm_CompressionLevel,
        ConvertMoviesForm_AudioBitrate,
        ConvertMoviesForm_OutputDirectory,
        MainForm_WindowMaximizeBehavior,
        MainForm_CompleteWindowState,
    }

    public Preferences()
    {
        _defaults = new Dictionary<Key, object>
        {
            [Key.Shared_UseShuffle] = 1L,
            [Key.Shared_VlcInstallationToUse] = VlcInstallationToUse.Automatic.ToString(),
            [Key.ConvertMoviesForm_VideoQuality] = "17 (recommended)",
            [Key.ConvertMoviesForm_CompressionLevel] = "slow (recommended)",
            [Key.ConvertMoviesForm_AudioBitrate] = "256 kbps (recommended)",
            [Key.ConvertMoviesForm_OutputDirectory] = "",
            [Key.MainForm_WindowMaximizeBehavior] = WindowMaximizeBehavior.Fullscreen.ToString(),
            [Key.MainForm_CompleteWindowState] = """
                {
                    "WindowState": 2,
                    "UnscaledRestoreBounds": {},
                    "UnscaledLocation": {},
                    "UnscaledSize": {}
                }
                """,
        }.ToFrozenDictionary();

        // Make sure every default is one of the four supported types.
        foreach (var value in _defaults.Values)
            if (value is not (long or double or string or byte[]))
                throw new InvalidOperationException($"Unsupported default value type: {value.GetType()}");

        // Make sure every key has a default value.
        foreach (Key key in Enum.GetValues<Key>())
            if (!_defaults.ContainsKey(key))
                throw new InvalidOperationException($"No default value for preference \"{key}\".");
    }

    public void Dispose()
    {
        Disconnect();
    }

    public void Disconnect()
    {
        lock (_lock)
        {
            _connection?.Dispose();
            _connection = null;
        }
    }

    public void SetBoolean(Key key, bool value) => Set(key, value ? 1L : 0L);

    public void SetInteger(Key key, long value) => Set(key, value);

    public void SetReal(Key key, double value) => Set(key, value);

    public void SetText(Key key, string value) => Set(key, value);

    public void SetBlob(Key key, byte[] value) => Set(key, value);

    public void SetEnum<T>(Key key, T value)
        where T : struct => Set(key, value.ToString()!);

    private void Set(Key key, object value)
    {
        var expectedType = _defaults[key].GetType();
        if (value.GetType() != expectedType)
            throw new ArgumentException($"{nameof(value)} must be of type {expectedType.Name}.");

        lock (_lock)
        {
            Connect();
            using var command = _connection!.CreateCommand();
            command.CommandText = "INSERT OR REPLACE INTO preferences (key, value) VALUES (@key, @value);";
            command.Parameters.AddWithValue("@key", key.ToString());
            command.Parameters.AddWithValue("@value", value);
            command.ExecuteNonQuery();
        }
    }

    public void WithTransaction(Action action)
    {
        lock (_lock)
        {
            Connect();
            using var transaction = _connection!.BeginTransaction();
            action();
            transaction.Commit();
        }
    }

    public bool GetBoolean(Key key) => Get<long>(key) != 0;

    public long GetInteger(Key key) => Get<long>(key);

    public double GetReal(Key key) => Get<double>(key);

    public string GetText(Key key) => Get<string>(key);

    public byte[] GetBlob(Key key) => Get<byte[]>(key);

    public T GetEnum<T>(Key key)
        where T : struct
    {
        if (Enum.TryParse<T>(Get<string>(key), out var parsed))
            return parsed;
        return Enum.Parse<T>((string)_defaults[key]);
    }

    private T Get<T>(Key key)
    {
        try
        {
            lock (_lock)
            {
                Connect();
                using var command = _connection!.CreateCommand();
                command.CommandText = "SELECT value FROM preferences WHERE key = @key;";
                command.Parameters.AddWithValue("@key", key.ToString());
                using var reader = command.ExecuteReader();
                return reader.Read() && reader[0] is T t ? t : (T)_defaults[key];
            }
        }
        catch
        {
            return (T)_defaults[key];
        }
    }

    private void Connect()
    {
        if (_connection is not null)
            return;

        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Jackpot",
            "preferences.db"
        );

        try
        {
            _connection = Connect(path);
        }
        catch when (File.Exists(path))
        {
            File.Delete(path);
            _connection = Connect(path);
        }
    }

    private static SqliteConnection Connect(string path)
    {
        SqliteConnectionStringBuilder builder = new() { DataSource = path, Pooling = false };
        SqliteConnection connection = new(builder.ConnectionString);
        connection.Open();

        using (var command = connection!.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS preferences (
                    key TEXT PRIMARY KEY,
                    value
                );
                """;
            command.ExecuteNonQuery();
        }

        return connection;
    }
}
