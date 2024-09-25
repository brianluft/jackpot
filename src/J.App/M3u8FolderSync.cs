using J.Core;
using J.Core.Data;

namespace J.App;

public sealed class M3u8FolderSync(
    AccountSettingsProvider accountSettingsProvider,
    LibraryProviderAdapter libraryProvider
)
{
    private static readonly string _filesystemInvalidChars =
        new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());

    public bool Enabled => accountSettingsProvider.Current.EnableLocalM3u8Folder;

    public void Sync()
    {
        if (!Enabled)
            return;

        var dir = accountSettingsProvider.Current.LocalM3u8FolderPath;

        var movieTags = libraryProvider.GetMovieTags().ToLookup(x => x.TagId, x => x.MovieId);
        var movies = libraryProvider.GetMovies().ToDictionary(x => x.Id);
        HashSet<string> livingFiles = []; // locked, updated inside Sync calls below

        var existingFiles = Directory
            .GetFiles(dir, "*.m3u8", SearchOption.AllDirectories)
            .Select(x => x.ToUpperInvariant())
            .ToHashSet(); // read-only during Sync calls below

        foreach (var tagType in libraryProvider.GetTagTypes())
        {
            var tagDir = Path.Combine(dir, MakeFilesystemSafe(tagType.PluralName));
            Directory.CreateDirectory(tagDir);
            SyncTagType(tagDir, tagType, movies, movieTags, existingFiles, livingFiles);
        }

        lock (livingFiles)
        {
            existingFiles.ExceptWith(livingFiles);
            foreach (var fp in existingFiles)
                File.Delete(fp);
        }
    }

    public void SyncMovies(
        string dir,
        IEnumerable<Movie> movies,
        HashSet<string> existingFiles,
        HashSet<string> livingFiles
    )
    {
        Parallel.ForEach(
            movies,
            new ParallelOptions { MaxDegreeOfParallelism = 4 },
            movie =>
            {
                CopyM3u8(dir, movie, existingFiles, livingFiles);
            }
        );
    }

    public void SyncTagType(
        string dir,
        TagType tagType,
        Dictionary<MovieId, Movie> movies,
        ILookup<TagId, MovieId> movieTags,
        HashSet<string> existingFiles,
        HashSet<string> livingFiles
    )
    {
        foreach (var tag in libraryProvider.GetTags(tagType.Id))
        {
            var tagDir = Path.Combine(dir, MakeFilesystemSafe(tag.Name));
            Directory.CreateDirectory(tagDir);
            foreach (var movieId in movieTags[tag.Id])
            {
                var movie = movies[movieId];
                CopyM3u8(tagDir, movie, existingFiles, livingFiles);
            }
        }
    }

    private void CopyM3u8(string dir, Movie movie, HashSet<string> existingFiles, HashSet<string> livingFiles)
    {
        var m3u8FilePath = Path.Combine(
            dir,
            MakeFilesystemSafe(Path.GetFileNameWithoutExtension(movie.Filename)) + ".m3u8"
        );

        lock (livingFiles)
        {
            livingFiles.Add(m3u8FilePath.ToUpperInvariant());
        }

        if (existingFiles.Contains(m3u8FilePath.ToUpperInvariant()))
            return;

        var m3u8 = libraryProvider.GetM3u8(movie.Id);

        File.WriteAllBytes(m3u8FilePath, m3u8);
    }

    private static string MakeFilesystemSafe(string name) =>
        string.Join("_", name.Split(_filesystemInvalidChars, StringSplitOptions.RemoveEmptyEntries));
}
