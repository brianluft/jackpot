using System.Globalization;
using J.Core;
using J.Core.Data;

namespace J.App;

public sealed partial class M3u8FolderSync(LibraryProvider libraryProvider, Client client, Preferences preferences)
{
    private const int MAX_PATH = 260;

    private static readonly string _filesystemInvalidChars =
        new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());

    private readonly Lock _lock = new();
    private readonly List<string> _log = [];
    private readonly HashSet<TagTypeId> _invalidatedTagTypes = [];
    private readonly HashSet<TagId> _invalidatedTags = [];
    private readonly HashSet<MovieId> _invalidatedMovies = [];
    private bool _invalidatedAll = true;

    public bool Enabled => preferences.GetBoolean(Preferences.Key.NetworkSharing_AllowVlcAccess);

    public void Invalidate(
        IEnumerable<TagTypeId>? tagTypes = null,
        IEnumerable<TagId>? tags = null,
        IEnumerable<MovieId>? movies = null
    )
    {
        if (!Enabled)
            return;

        lock (_lock)
        {
            if (tagTypes is not null)
                foreach (var tagType in tagTypes)
                    _invalidatedTagTypes.Add(tagType);

            if (tags is not null)
                foreach (var tag in tags)
                    _invalidatedTags.Add(tag);

            if (movies is not null)
                foreach (var movie in movies)
                    _invalidatedMovies.Add(movie);
        }
    }

    public void InvalidateAll()
    {
        // Still invalidate even if sync is disabled, so that the next time it's enabled, it will sync everything.

        lock (_lock)
        {
            _invalidatedAll = true;
        }
    }

    public void Sync(Action<double> updateProgress, CancellationToken cancel)
    {
        if (!Enabled)
            return;

        var dir = preferences.GetText(Preferences.Key.NetworkSharing_VlcFolderPath);
        var errorFilePath = Path.Combine(dir, "Sync Error.txt");

        lock (_lock)
        {
            _log.Clear();
        }

        try
        {
            File.Delete(errorFilePath);
            SyncCore(dir, updateProgress, cancel);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            try
            {
                File.AppendAllText(errorFilePath, ex.ToString());
            }
            catch
            {
                // Throw the original exception.
            }
        }
        finally
        {
            try
            {
                lock (_lock)
                {
                    if (_log.Count > 0)
                        File.AppendAllText(errorFilePath, Environment.NewLine + string.Join(Environment.NewLine, _log));
                }
            }
            catch { }
        }
    }

    public void SyncCore(string dir, Action<double> updateProgress, CancellationToken cancel)
    {
        lock (_lock)
        {
            cancel.ThrowIfCancellationRequested();

            var portNumber = client.Port;
            var sessionPassword = client.SessionPassword;

            var movieTags = libraryProvider.GetMovieTags().ToLookup(x => x.TagId, x => x.MovieId);
            var movies = libraryProvider.GetMovies().ToDictionary(x => x.Id);
            HashSet<string> livingFiles = []; // locked, updated inside Sync calls below

            cancel.ThrowIfCancellationRequested();

            var existingFiles = Directory
                .GetFiles(dir, "*.m3u8", SearchOption.AllDirectories)
                .Select(x => x.ToUpperInvariant())
                .ToHashSet();

            cancel.ThrowIfCancellationRequested();

            var moviesDir = Path.Combine(dir, "Movies");
            Directory.CreateDirectory(moviesDir);
            SyncMovies(
                moviesDir,
                movies,
                livingFiles,
                portNumber,
                sessionPassword,
                x => updateProgress(0.5 * x),
                cancel
            );

            var tagTypes = libraryProvider.GetTagTypes();
            var progressPerTagType = 0.5d / tagTypes.Count;
            for (var i = 0; i < tagTypes.Count; i++)
            {
                var tagType = tagTypes[i];
                var tagDir = Path.Combine(dir, Truncate(RemoveInvalidFilesystemCharacters(tagType.PluralName), 30));
                if (tagDir.Length >= MAX_PATH)
                {
                    Log("Tag group path is too long: " + tagDir);
                    continue;
                }

                Directory.CreateDirectory(tagDir);
                SyncTagType(
                    tagDir,
                    tagType,
                    movies,
                    movieTags,
                    livingFiles,
                    portNumber,
                    sessionPassword,
                    x => updateProgress(0.5 + progressPerTagType * i + x * progressPerTagType),
                    cancel
                );
                cancel.ThrowIfCancellationRequested();
            }

            updateProgress(1);

            lock (livingFiles)
            {
                cancel.ThrowIfCancellationRequested();

                existingFiles.ExceptWith(livingFiles);
                foreach (var fp in existingFiles)
                    File.Delete(fp);
            }

            DeleteEmptyDirectories(dir);

            cancel.ThrowIfCancellationRequested();

            _invalidatedTagTypes.Clear();
            _invalidatedTags.Clear();
            _invalidatedMovies.Clear();
            _invalidatedAll = false;
        }
    }

    private void DeleteEmptyDirectories(string dir)
    {
        foreach (var subDir in Directory.GetDirectories(dir))
        {
            DeleteEmptyDirectories(subDir);
            if (Directory.GetFileSystemEntries(subDir).Length == 0)
            {
                Directory.Delete(subDir);
            }
        }
    }

    private void SyncMovies(
        string dir,
        Dictionary<MovieId, Movie> movies,
        HashSet<string> livingFiles,
        int portNumber,
        string sessionPassword,
        Action<double> updateProgress,
        CancellationToken cancel
    )
    {
        var soFar = 0; // interlocked
        var count = movies.Count;
        var interval = Math.Max(1, count / 100);

        Parallel.ForEach(
            movies,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount - 1, CancellationToken = cancel },
            movie =>
            {
                CopyM3u8(
                    dir,
                    movie.Value,
                    livingFiles,
                    portNumber,
                    sessionPassword,
                    writeFile: _invalidatedMovies.Contains(movie.Key) || _invalidatedAll
                );

                var i = Interlocked.Increment(ref soFar);
                if (i % interval == 0)
                    updateProgress((double)i / count);
            }
        );
    }

    private void SyncTagType(
        string dir,
        TagType tagType,
        Dictionary<MovieId, Movie> movies,
        ILookup<TagId, MovieId> movieTags,
        HashSet<string> livingFiles,
        int portNumber,
        string sessionPassword,
        Action<double> updateProgress,
        CancellationToken cancel
    )
    {
        List<(TagId TagId, string TagDir, Movie Movie)> files = [];

        foreach (var tag in libraryProvider.GetTags(tagType.Id))
        {
            cancel.ThrowIfCancellationRequested();
            var tagDir = Path.Combine(dir, Truncate(RemoveInvalidFilesystemCharacters(tag.Name), 50));

            if (tagDir.Length >= MAX_PATH)
            {
                Log("Tag path is too long: " + tagDir);
                continue;
            }

            Directory.CreateDirectory(tagDir);
            foreach (var movieId in movieTags[tag.Id])
            {
                var movie = movies[movieId];
                files.Add((tag.Id, tagDir, movie));
            }
        }

        var soFar = 0; // interlocked
        var count = files.Count;
        var interval = Math.Max(1, count / 100);

        Parallel.ForEach(
            files,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount - 1, CancellationToken = cancel },
            file =>
            {
                CopyM3u8(
                    file.TagDir,
                    file.Movie,
                    livingFiles,
                    portNumber,
                    sessionPassword,
                    writeFile: _invalidatedTags.Contains(file.TagId)
                        || _invalidatedMovies.Contains(file.Movie.Id)
                        || _invalidatedAll
                );

                var i = Interlocked.Increment(ref soFar);
                if (i % interval == 0)
                    updateProgress((double)i / count);
            }
        );
    }

    private void CopyM3u8(
        string dir,
        Movie movie,
        HashSet<string> livingFiles,
        int portNumber,
        string sessionPassword,
        bool writeFile
    )
    {
        string m3u8FilePath;
        lock (livingFiles)
        {
            // Try: "prefix.m3u8", "prefix 2.m3u8", "prefix 3.m3u8", etc.
            var i = 1;

            while (true)
            {
                var suffix = (i == 1 ? "" : $" {i}") + ".m3u8";

                var prefix = Truncate(
                    Path.Combine(dir, RemoveInvalidFilesystemCharacters(movie.Filename)),
                    259 - suffix.Length
                );

                if (prefix.Length <= dir.Length)
                {
                    Log("Movie path is too long: " + dir);
                    return;
                }

                m3u8FilePath = prefix + suffix;

                if (livingFiles.Add(m3u8FilePath.ToUpperInvariant()))
                    break;

                i++;
            }
        }

        if (writeFile)
        {
            var hostname = preferences.GetText(Preferences.Key.NetworkSharing_Hostname);
            var bytes = libraryProvider.GetM3u8(movie.Id, portNumber, sessionPassword, hostname);
            File.WriteAllBytes(m3u8FilePath, bytes);
        }
    }

    private static string RemoveInvalidFilesystemCharacters(string name) =>
        string.Join("_", name.Split(_filesystemInvalidChars, StringSplitOptions.RemoveEmptyEntries));

    private static string Truncate(string text, int maxCodeUnits)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        if (maxCodeUnits <= 0)
            return string.Empty;

        // If the text is already within limits, return it as-is
        if (text.Length <= maxCodeUnits)
            return text;

        var currentLength = 0;
        var lastValidPosition = 0;
        var currentPosition = 0;

        // Enumerate through grapheme clusters
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            var currentCluster = enumerator.GetTextElement();
            var clusterLength = currentCluster.Length; // Length in UTF-16 code units

            // Check if adding this cluster would exceed our limit
            if (currentLength + clusterLength > maxCodeUnits)
                break;

            currentLength += clusterLength;
            lastValidPosition = currentPosition;
            currentPosition += currentCluster.Length;
        }

        // Return the substring up to the last valid position
        return text[..lastValidPosition];
    }

    private void Log(string message)
    {
        lock (_lock)
        {
            _log.Add(message);
        }
    }
}
