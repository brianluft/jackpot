using System.Collections.Frozen;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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

    private readonly FrozenSet<string> _stopWords = new[]
    {
        "a",
        "am",
        "an",
        "and",
        "are",
        "as",
        "at",
        "be",
        "by",
        "for",
        "from",
        "has",
        "he",
        "in",
        "is",
        "it",
        "its",
        "of",
        "on",
        "that",
        "the",
        "these",
        "there",
        "their",
        "they",
        "to",
        "was",
        "were",
        "will",
        "with",
    }.ToFrozenSet();

    private readonly object _syncLock = new();
    private readonly object _fileHashesLock = new();
    private readonly Dictionary<string, byte[]> _fileHashes = [];

    public bool Enabled => accountSettingsProvider.Current.EnableLocalM3u8Folder;

    public void Sync(Action<double> updateProgress)
    {
        if (!Enabled)
            return;

        lock (_syncLock)
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
                .ToHashSet(); // read-only during Sync calls below

            var moviesDir = Path.Combine(dir, "Movies");
            Directory.CreateDirectory(moviesDir);
            SyncMovies(moviesDir, movies, livingFiles, portNumber, sessionPassword, x => updateProgress(0.2 * x));

            var tagTypes = libraryProvider.GetTagTypes();
            var progressPerTagType = 0.2d / tagTypes.Count;
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
                    x => updateProgress(0.2 + progressPerTagType * i + x * progressPerTagType)
                );
            }

            var searchDir = Path.Combine(dir, "Search");
            Directory.CreateDirectory(searchDir);
            SyncSearch(searchDir, movies, livingFiles, portNumber, sessionPassword, x => updateProgress(0.4 + 0.6 * x));

            lock (livingFiles)
            {
                existingFiles.ExceptWith(livingFiles);
                foreach (var fp in existingFiles)
                    File.Delete(fp);
            }

            DeleteEmptyDirectories(dir);
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
                CopyM3u8(dir, movie.Value, livingFiles, portNumber, sessionPassword);

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
        List<(string TagDir, Movie Movie)> files = [];

        foreach (var tag in libraryProvider.GetTags(tagType.Id))
        {
            var tagDir = Path.Combine(dir, MakeFilesystemSafe(tag.Name));
            Directory.CreateDirectory(tagDir);
            foreach (var movieId in movieTags[tag.Id])
            {
                var movie = movies[movieId];
                files.Add((tagDir, movie));
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
                CopyM3u8(file.TagDir, file.Movie, livingFiles, portNumber, sessionPassword);

                var i = Interlocked.Increment(ref soFar);
                if (i % interval == 0)
                    updateProgress((double)i / count);
            }
        );
    }

    private void CopyM3u8(string dir, Movie movie, HashSet<string> livingFiles, int portNumber, string sessionPassword)
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

        var bytes = libraryProvider.GetM3u8(movie.Id, portNumber, sessionPassword);
        var fileHash = SHA256.HashData(bytes);

        lock (_fileHashesLock)
        {
            if (_fileHashes.TryGetValue(m3u8FilePath, out var x) && fileHash.SequenceEqual(x))
                return;
        }

        File.WriteAllBytes(m3u8FilePath, bytes);

        lock (_fileHashesLock)
        {
            _fileHashes[m3u8FilePath] = fileHash;
        }
    }

    private void SyncSearch(
        string searchDir,
        Dictionary<MovieId, Movie> movies,
        HashSet<string> livingFiles,
        int portNumber,
        string sessionPassword,
        Action<double> updateProgress
    )
    {
        var wordGroups = (
            from movie in movies.Values
            from rawWord in WordSeparatorRegex().Split(movie.Filename)
            let word = MakeFilesystemSafe(Strip(rawWord)).ToLowerInvariant()
            where word.Length >= 2 && !NumberRegex().IsMatch(word) && !_stopWords.Contains(word)
            group movie by word into wordGroup
            let distinctMovies = wordGroup.Distinct().ToList()
            orderby distinctMovies.Count descending
            select (Word: wordGroup.Key, Movies: distinctMovies)
        ).Take(500);

        List<(string Dir, Movie Movie)> files = [];

        foreach (var (word, wordMovies) in wordGroups)
        {
            var firstCharacter = GetFirstCharacter(word).ToUpperInvariant();
            var dir = Path.Combine(searchDir, firstCharacter, word);
            foreach (var movie in wordMovies)
            {
                files.Add((dir, movie));
            }
        }

        Parallel.ForEach(
            files.Select(x => x.Dir).Distinct(),
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount - 1 },
            dir => Directory.CreateDirectory(dir)
        );

        var soFar = 0; // interlocked
        var count = files.Count;
        var interval = Math.Max(1, count / 100);

        Parallel.ForEach(
            files,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount - 1 },
            file =>
            {
                CopyM3u8(file.Dir, file.Movie, livingFiles, portNumber, sessionPassword);

                var i = Interlocked.Increment(ref soFar);
                if (i % interval == 0)
                    updateProgress((double)i / count);
            }
        );
    }

    private static string GetFirstCharacter(string str)
    {
        var enumerator = StringInfo.GetTextElementEnumerator(str);
        if (enumerator.MoveNext())
            return enumerator.GetTextElement();

        throw new ArgumentException("Empty string.", nameof(str));
    }

    private static string Strip(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return str;
        }

        StringBuilder sb = new(str.Length);

        foreach (var c in str)
        {
            if (c is '\'')
            {
                // Allow "don't" etc.
                sb.Append(c);
                continue;
            }

            switch (CharUnicodeInfo.GetUnicodeCategory(c))
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.DecimalDigitNumber:
                case UnicodeCategory.LetterNumber:
                case UnicodeCategory.OtherNumber:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }

    private string MakeFilesystemSafe(string name) =>
        string.Join("_", name.Split(_filesystemInvalidChars, StringSplitOptions.RemoveEmptyEntries));

    [GeneratedRegex(@"[_'""\s\-\[\]\(\)]+")]
    private static partial Regex WordSeparatorRegex();

    [GeneratedRegex(@"^[\d\-\._]+$")]
    private static partial Regex NumberRegex();
}
