using System.Globalization;
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
    private static readonly string _filesystemInvalidChars =
        new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());

    private static readonly object _lock = new();

    public bool Enabled => accountSettingsProvider.Current.EnableLocalM3u8Folder;

    public void Sync()
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
                .ToHashSet(); // read-only during Sync calls below

            var moviesDir = Path.Combine(dir, "Movies");
            Directory.CreateDirectory(moviesDir);
            SyncMovies(moviesDir, movies.Values, livingFiles, portNumber, sessionPassword);

            foreach (var tagType in libraryProvider.GetTagTypes())
            {
                var tagDir = Path.Combine(dir, MakeFilesystemSafe(tagType.PluralName));
                Directory.CreateDirectory(tagDir);
                SyncTagType(tagDir, tagType, movies, movieTags, livingFiles, portNumber, sessionPassword);
            }

            var searchDir = Path.Combine(dir, "Search");
            Directory.CreateDirectory(searchDir);
            SyncSearch(searchDir, movies.Values, livingFiles, portNumber, sessionPassword);

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
        IEnumerable<Movie> movies,
        HashSet<string> livingFiles,
        int portNumber,
        string sessionPassword
    )
    {
        Parallel.ForEach(
            movies,
            new ParallelOptions { MaxDegreeOfParallelism = 4 },
            movie =>
            {
                CopyM3u8(dir, movie, livingFiles, portNumber, sessionPassword);
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
        string sessionPassword
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

        Parallel.ForEach(
            files,
            new ParallelOptions { MaxDegreeOfParallelism = 4 },
            file =>
            {
                CopyM3u8(file.TagDir, file.Movie, livingFiles, portNumber, sessionPassword);
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

        var m3u8 = libraryProvider.GetM3u8(movie.Id, portNumber, sessionPassword);

        File.WriteAllBytes(m3u8FilePath, m3u8);
    }

    private void SyncSearch(
        string searchDir,
        IEnumerable<Movie> movies,
        HashSet<string> livingFiles,
        int portNumber,
        string sessionPassword
    )
    {
        var wordGroups = (
            from movie in movies
            from rawWord in WhitespaceRegex().Split(movie.Filename)
            let word = MakeFilesystemSafe(Strip(rawWord))
            where word.Length >= 2 && !NumberRegex().IsMatch(word)
            select (Word: word.ToLowerInvariant(), Movie: movie)
        )
            .Distinct()
            .ToLookup(x => x.Word, x => x.Movie);

        List<(string Dir, Movie Movie)> files = [];

        foreach (var wordGroup in wordGroups)
        {
            var firstCharacter = GetFirstCharacter(wordGroup.Key).ToUpperInvariant();
            var dir = Path.Combine(searchDir, firstCharacter, wordGroup.Key);
            foreach (var movie in wordGroup)
            {
                files.Add((dir, movie));
            }
        }

        Parallel.ForEach(
            files,
            new ParallelOptions { MaxDegreeOfParallelism = 4 },
            file =>
            {
                Directory.CreateDirectory(file.Dir);
                CopyM3u8(file.Dir, file.Movie, livingFiles, portNumber, sessionPassword);
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
            switch (CharUnicodeInfo.GetUnicodeCategory(c))
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.OtherLetter:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }

    private static string MakeFilesystemSafe(string name) =>
        string.Join("_", name.Split(_filesystemInvalidChars, StringSplitOptions.RemoveEmptyEntries));

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^[\d\-\._]+$")]
    private static partial Regex NumberRegex();
}
