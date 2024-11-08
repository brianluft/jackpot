using J.Core;
using J.Core.Data;

namespace J.App;

public sealed partial class M3u8FolderSync(
    AccountSettingsProvider accountSettingsProvider,
    LibraryProvider libraryProvider,
    Client client
)
{
    private readonly string _filesystemInvalidChars =
        new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());

    private readonly object _lock = new();
    private readonly HashSet<TagTypeId> _invalidatedTagTypes = [];
    private readonly HashSet<TagId> _invalidatedTags = [];
    private readonly HashSet<MovieId> _invalidatedMovies = [];
    private bool _invalidatedAll = true;

    public bool Enabled => accountSettingsProvider.Current.EnableLocalM3u8Folder;

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
        if (!Enabled)
            return;

        lock (_lock)
        {
            _invalidatedAll = true;
        }
    }

    public void Sync(Action<double> updateProgress)
    {
        if (!Enabled)
            return;

        lock (_lock)
        {
            var portNumber = client.Port;
            var sessionPassword = client.SessionPassword;
            var dir = accountSettingsProvider.Current.LocalM3u8FolderPath;

            var movieTags = libraryProvider.GetMovieTags().ToLookup(x => x.TagId, x => x.MovieId);
            var movies = libraryProvider.GetMovies().ToDictionary(x => x.Id);
            HashSet<string> livingFiles = []; // locked, updated inside Sync calls below

            var existingFiles = Directory
                .GetFiles(dir, "*.m3u8", SearchOption.AllDirectories)
                .Select(x => x.ToUpperInvariant())
                .ToHashSet();

            var moviesDir = Path.Combine(dir, "Movies");
            Directory.CreateDirectory(moviesDir);
            SyncMovies(moviesDir, movies, livingFiles, portNumber, sessionPassword, x => updateProgress(0.5 * x));

            var tagTypes = libraryProvider.GetTagTypes();
            var progressPerTagType = 0.5d / tagTypes.Count;
            for (var i = 0; i < tagTypes.Count; i++)
            {
                var tagType = tagTypes[i];
                var tagDir = Path.Combine(dir, MakeFilesystemSafe(tagType.PluralName));
                Directory.CreateDirectory(tagDir);
                SyncTagType(
                    tagDir,
                    tagType,
                    movies,
                    movieTags,
                    livingFiles,
                    portNumber,
                    sessionPassword,
                    x => updateProgress(0.5 + progressPerTagType * i + x * progressPerTagType)
                );
            }

            updateProgress(1);

            lock (livingFiles)
            {
                existingFiles.ExceptWith(livingFiles);
                foreach (var fp in existingFiles)
                    File.Delete(fp);
            }

            DeleteEmptyDirectories(dir);

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
        Action<double> updateProgress
    )
    {
        var soFar = 0; // interlocked
        var count = movies.Count;
        var interval = Math.Max(1, count / 100);

        Parallel.ForEach(
            movies,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount - 1 },
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
        Action<double> updateProgress
    )
    {
        List<(TagId TagId, string TagDir, Movie Movie)> files = [];

        foreach (var tag in libraryProvider.GetTags(tagType.Id))
        {
            var tagDir = Path.Combine(dir, MakeFilesystemSafe(tag.Name));
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
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount - 1 },
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
        var prefix = Path.Combine(dir, MakeFilesystemSafe(movie.Filename));

        var m3u8FilePath = prefix + ".m3u8";

        lock (livingFiles)
        {
            // Try: "prefix.m3u8", "prefix 2.m3u8", "prefix 3.m3u8", etc.
            var i = 1;
            while (!livingFiles.Add(m3u8FilePath.ToUpperInvariant()))
            {
                i++;
                m3u8FilePath = $"{prefix} {i}.m3u8";
            }
        }

        if (writeFile)
        {
            var bytes = libraryProvider.GetM3u8(movie.Id, portNumber, sessionPassword);
            File.WriteAllBytes(m3u8FilePath, bytes);
        }
    }

    private string MakeFilesystemSafe(string name) =>
        string.Join("_", name.Split(_filesystemInvalidChars, StringSplitOptions.RemoveEmptyEntries));
}
