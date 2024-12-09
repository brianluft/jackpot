using System.Collections.Frozen;
using System.Data;
using J.Core;

namespace J.App;

public sealed class ImportQueue : IDisposable
{
    private const int NUM_TASKS = 2;
    private readonly LibraryProviderAdapter _libraryProvider;
    private readonly ProcessTempDir _processTempDir;
    private readonly Preferences _preferences;
    private readonly Importer _importer;
    private CancellationTokenSource? _cts = null;
    private Task[] _tasks = [];
    private bool _disposed;

    private static readonly FrozenSet<string> _extensions = new[]
    {
        ".3g2",
        ".3gp",
        ".asf",
        ".avi",
        ".divx",
        ".f4a",
        ".f4b",
        ".f4p",
        ".f4v",
        ".flv",
        ".m1v",
        ".m2t",
        ".m2ts",
        ".m2v",
        ".m4v",
        ".mks",
        ".mkv",
        ".mov",
        ".mp4",
        ".mpd",
        ".mpe",
        ".mpeg",
        ".mpg",
        ".mpv",
        ".mts",
        ".ogg",
        ".ogv",
        ".qt",
        ".ts",
        ".webm",
        ".wmv",
    }.ToFrozenSet();

    public Lock DataTableLock { get; } = new();
    public DataTable DataTable { get; }
    public event EventHandler? FileCompleted;

    public bool IsRunning { get; private set; }
    public event EventHandler? IsRunningChanged;

    public ImportQueue(
        LibraryProviderAdapter libraryProvider,
        ProcessTempDir processTempDir,
        Preferences preferences,
        Importer importer
    )
    {
        _libraryProvider = libraryProvider;
        _processTempDir = processTempDir;
        _preferences = preferences;
        _importer = importer;
        DataTable = new();
        {
            DataTable.Columns.Add("file_path", typeof(string));
            DataTable.Columns.Add("filename", typeof(string));
            DataTable.Columns.Add("message", typeof(string));
            DataTable.Columns.Add("progress", typeof(double));
            DataTable.Columns.Add("state", typeof(FileState));
            DataTable.Columns.Add("error", typeof(string));
            DataTable.Columns.Add("size_mb", typeof(double));
        }
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            // free unmanaged resources (unmanaged objects) and override finalizer
            _cts?.Cancel();
            foreach (var t in _tasks)
                t.Wait();
            _cts?.Dispose();
            _cts = null;
            foreach (var t in _tasks)
                t.Dispose();
            _tasks = [];

            if (disposing)
            {
                // dispose managed state (managed objects)
                lock (DataTableLock)
                {
                    DataTable.Dispose();
                }
            }

            _disposed = true;
        }
    }

    ~ImportQueue()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public int Count
    {
        get
        {
            lock (DataTableLock)
            {
                return DataTable.Rows.Count;
            }
        }
    }

    public void Clear()
    {
        lock (DataTableLock)
        {
            // Clear any rows in the Success state.
            var any = false;
            for (var i = DataTable.Rows.Count - 1; i >= 0; i--)
            {
                if ((FileState)DataTable.Rows[i]["state"] == FileState.Success)
                {
                    DataTable.Rows.RemoveAt(i);
                    any = true;
                }
            }

            // If there weren't any successful rows, then delete everything.
            if (!any)
                DataTable.Rows.Clear();
        }
    }

    public void AddFiles(IEnumerable<string> filePaths)
    {
        lock (DataTableLock)
        {
            DataTable.BeginLoadData();
            try
            {
                var queuedFilenames = DataTable
                    .Rows.Cast<DataRow>()
                    .Select(row => row["filename"].ToString())
                    .ToHashSet();

                foreach (var filePath in filePaths)
                {
                    var filename = Path.GetFileNameWithoutExtension(filePath);

                    if (!_extensions.Contains(Path.GetExtension(filePath).ToLowerInvariant()))
                        continue;

                    if (_libraryProvider.MovieExists(filename))
                        continue;

                    if (queuedFilenames.Contains(filename))
                        continue;

                    var row = DataTable.NewRow();
                    row["file_path"] = filePath;
                    row["filename"] = filename;
                    row["message"] = "";
                    row["progress"] = 0d;
                    row["state"] = FileState.Pending;
                    row["error"] = DBNull.Value;
                    row["size_mb"] = new FileInfo(filePath).Length / 1024d / 1024d;
                    DataTable.Rows.Add(row);

                    queuedFilenames.Add(filename);
                }
            }
            finally
            {
                DataTable.EndLoadData();
            }
        }
    }

    public void Start()
    {
        if (IsRunning)
            throw new InvalidOperationException("Already started.");

        IsRunning = true;
        IsRunningChanged?.Invoke(this, EventArgs.Empty);

        // Set all Failed to Pending so we will try them again.
        lock (DataTableLock)
        {
            foreach (DataRow row in DataTable.Rows)
            {
                if ((FileState)row["state"] == FileState.Failed)
                    row["state"] = FileState.Pending;
            }
        }

        _cts = new();
        _tasks = new Task[NUM_TASKS];
        for (var i = 0; i < NUM_TASKS; i++)
            _tasks[i] = Task.Run(() => RunTask(_cts.Token));

        Task.WhenAll(_tasks).ContinueWith(_ => Stop());
    }

    public void Stop()
    {
        if (!IsRunning)
            throw new InvalidOperationException("Already stopped.");

        _cts?.Cancel();
        foreach (var t in _tasks)
            t.Wait();
        _tasks = [];
        _cts?.Dispose();
        _cts = null;

        IsRunning = false;
        IsRunningChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RunTask(CancellationToken cancel)
    {
        try
        {
            while (!cancel.IsCancellationRequested)
            {
                var row = GetNextRow();
                if (row is null)
                    break;

                ImportFile(row, cancel);
            }
        }
        catch (OperationCanceledException) { }
    }

    private DataRow? GetNextRow()
    {
        DataRow? nextRow = null;

        lock (DataTableLock)
        {
            foreach (DataRow row in DataTable.Rows)
            {
                if ((FileState)row["state"] == FileState.Pending)
                {
                    nextRow = row;
                    break;
                }
            }

            if (nextRow is not null)
                nextRow["state"] = FileState.Working;
        }

        return nextRow;
    }

    private void ImportFile(DataRow row, CancellationToken cancel)
    {
        var filePath = (string)row["file_path"];
        try
        {
            UpdateRow(row, FileState.Working, "Starting", 0d);
            using var tempDir = _processTempDir.NewDir();
            var convertedFilePath = Path.Combine(tempDir.Path, Path.GetFileNameWithoutExtension(filePath) + ".mp4");

            cancel.ThrowIfCancellationRequested();
            UpdateRow(row, FileState.Working, "Inspecting", 0d);
            var needsConversion = !Ffmpeg.IsCompatibleCodec(filePath);

            ImportProgress importProgress =
                new(progress => UpdateRowProgress(row, progress), message => UpdateRowMessage(row, message));

            cancel.ThrowIfCancellationRequested();
            if (needsConversion)
            {
                if (!_preferences.GetBoolean(Preferences.Key.ImportControl_AutoConvert))
                {
                    throw new Exception(
                        $"The file \"{Path.GetFileName(filePath)}\" uses an incompatible movie format."
                    );
                }

                importProgress.UpdateMessage("Waiting to re-encode");
                lock (GlobalLocks.BigCpu)
                {
                    cancel.ThrowIfCancellationRequested();
                    importProgress.UpdateProgress(ImportProgress.Phase.Converting, 0);

                    MovieConverter.Convert(
                        filePath,
                        convertedFilePath,
                        int.Parse(_preferences.GetText(Preferences.Key.ImportControl_VideoQuality).Split(' ')[0]),
                        _preferences.GetText(Preferences.Key.ImportControl_CompressionLevel).Split(' ')[0],
                        int.Parse(_preferences.GetText(Preferences.Key.ImportControl_AudioBitrate).Split(' ')[0]),
                        progress => importProgress.UpdateProgress(ImportProgress.Phase.Converting, progress),
                        cancel
                    );
                }
            }

            cancel.ThrowIfCancellationRequested();
            _importer.Import(needsConversion ? convertedFilePath : filePath, importProgress, cancel);

            UpdateRow(row, FileState.Success, "✔ Imported", 1d);
        }
        catch (Exception ex) when (ex is OperationCanceledException || cancel.IsCancellationRequested)
        {
            UpdateRow(row, FileState.Pending, "", 0d);
        }
        catch (Exception ex)
        {
            UpdateRow(row, FileState.Failed, "⚠️ Failed (click for details)", 0d, ex.Message);
        }
        finally
        {
            FileCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateRowProgress(DataRow row, double progress)
    {
        lock (DataTableLock)
        {
            row["progress"] = progress;
        }
    }

    private void UpdateRowMessage(DataRow row, string message)
    {
        lock (DataTableLock)
        {
            row["message"] = message;
        }
    }

    private void UpdateRow(DataRow row, FileState state, string message, double progress, string? error = null)
    {
        lock (DataTableLock)
        {
            row["state"] = state;
            row["message"] = message;
            row["progress"] = progress;
            row["error"] = error;
        }
    }

    public enum FileState
    {
        Pending,
        Working,
        Success,
        Failed,
    }
}
