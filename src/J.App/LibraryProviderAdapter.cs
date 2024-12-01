using J.Core;
using J.Core.Data;

namespace J.App;

public sealed class LibraryProviderAdapter(LibraryProvider libraryProvider, M3u8FolderSync m3U8FolderSync)
{
    private static readonly SemaphoreSlim _mutateLock = new(1, 1);

    public void Disconnect() => libraryProvider.Disconnect();

    private async Task MutateAsync(Action action, Action<double> updateProgress, CancellationToken cancel)
    {
        await _mutateLock.WaitAsync(cancel).ConfigureAwait(false);
        try
        {
            await libraryProvider.SyncDownAsync(x => updateProgress(0.4 * x), cancel).ConfigureAwait(false);
            action();
            await libraryProvider.SyncUpAsync(x => updateProgress(0.4 + 0.4 * x), cancel).ConfigureAwait(false);
            m3U8FolderSync.Sync(x => updateProgress(0.8 + 0.2 * x));
        }
        finally
        {
            _mutateLock.Release();
        }
    }

    public async Task NewTagAsync(Tag tag, Action<double> updateProgress, CancellationToken cancel)
    {
        m3U8FolderSync.Invalidate(tags: [tag.Id]);
        await MutateAsync(() => libraryProvider.NewTag(tag), updateProgress, cancel).ConfigureAwait(false);
    }

    public async Task UpdateTagAsync(Tag tag, Action<double> updateProgress, CancellationToken cancel)
    {
        m3U8FolderSync.Invalidate(tags: [tag.Id]);
        await MutateAsync(() => libraryProvider.UpdateTag(tag), updateProgress, cancel).ConfigureAwait(false);
    }

    public Tag GetTag(TagId id) => libraryProvider.GetTag(id);

    public async Task DeleteTagsAsync(List<TagId> ids, Action<double> updateProgress, CancellationToken cancel)
    {
        m3U8FolderSync.Invalidate(tags: ids);
        await MutateAsync(
                () =>
                {
                    foreach (var id in ids)
                        libraryProvider.DeleteTag(id);
                },
                updateProgress,
                cancel
            )
            .ConfigureAwait(false);
    }

    public List<Movie> GetMovies() => libraryProvider.GetMovies();

    public int GetDeletedMovieCount() => libraryProvider.GetDeletedMovieCount();

    public Movie GetMovie(MovieId id) => libraryProvider.GetMovie(id);

    public byte[] GetMovieClip(MovieId movieId) => libraryProvider.GetMovieClip(movieId);

    public async Task NewTagTypeAsync(TagType tagType, Action<double> updateProgress, CancellationToken cancel)
    {
        m3U8FolderSync.Invalidate(tagTypes: [tagType.Id]);
        await MutateAsync(() => libraryProvider.NewTagType(tagType), updateProgress, cancel).ConfigureAwait(false);
    }

    public List<TagType> GetTagTypes() => libraryProvider.GetTagTypes();

    public TagType GetTagType(TagTypeId tagTypeId) => libraryProvider.GetTagType(tagTypeId);

    public async Task DeleteTagTypeAsync(TagTypeId tagTypeId, Action<double> updateProgress, CancellationToken cancel)
    {
        m3U8FolderSync.Invalidate(tagTypes: [tagTypeId]);
        await MutateAsync(() => libraryProvider.DeleteTagType(tagTypeId), updateProgress, cancel).ConfigureAwait(false);
    }

    public async Task UpdateTagTypesAsync(
        List<TagType> tagTypes,
        Action<double> updateProgress,
        CancellationToken cancel
    )
    {
        m3U8FolderSync.Invalidate(tagTypes: tagTypes.Select(x => x.Id));
        await MutateAsync(
                () =>
                {
                    foreach (var tagType in tagTypes)
                        libraryProvider.UpdateTagType(tagType);
                },
                updateProgress,
                cancel
            )
            .ConfigureAwait(false);
    }

    public List<Tag> GetTags() => libraryProvider.GetTags();

    public List<Tag> GetTags(TagTypeId tagTypeId) => libraryProvider.GetTags(tagTypeId);

    public List<MovieTag> GetMovieTags() => libraryProvider.GetMovieTags();

    public List<MovieTag> GetMovieTags(MovieId movieId) => libraryProvider.GetMovieTags(movieId);

    public async Task AddMovieTagsAsync(
        List<(MovieId MovieId, TagId TagId)> list,
        Action<double> updateProgress,
        CancellationToken cancel
    )
    {
        m3U8FolderSync.Invalidate(tags: list.Select(x => x.TagId).Distinct());
        await WithTransactionAsync(
                () =>
                {
                    foreach (var (movieId, tagId) in list)
                        libraryProvider.AddMovieTag(movieId, tagId);
                },
                updateProgress,
                cancel
            )
            .ConfigureAwait(false);
    }

    public async Task DeleteMovieTagsAsync(List<MovieTag> list, Action<double> updateProgress, CancellationToken cancel)
    {
        m3U8FolderSync.Invalidate(tags: list.Select(x => x.TagId).Distinct());
        await WithTransactionAsync(
                () =>
                {
                    foreach (var (movieId, tagId) in list)
                        libraryProvider.DeleteMovieTag(movieId, tagId);
                },
                updateProgress,
                cancel
            )
            .ConfigureAwait(false);
    }

    public async Task NewMovieAsync(
        Movie movie,
        List<MovieFile> files,
        Action<double> updateProgress,
        CancellationToken cancel
    )
    {
        m3U8FolderSync.Invalidate(movies: [movie.Id]);
        await WithTransactionAsync(
                () =>
                {
                    libraryProvider.NewMovie(movie);
                    foreach (var files in files.Chunk(50))
                        libraryProvider.NewMovieFiles(files);
                },
                updateProgress,
                cancel
            )
            .ConfigureAwait(false);
    }

    public async Task UpdateMovieAsync(Movie movie, Action<double> updateProgress, CancellationToken cancel)
    {
        m3U8FolderSync.Invalidate(movies: [movie.Id]);
        await MutateAsync(() => libraryProvider.UpdateMovie(movie), updateProgress, cancel).ConfigureAwait(false);
    }

    public async Task UpdateMovieAsync(
        Movie movie,
        List<TagId> tagIds,
        Action<double> updateProgress,
        CancellationToken cancel
    )
    {
        m3U8FolderSync.Invalidate(movies: [movie.Id]);
        await MutateAsync(
                () =>
                {
                    libraryProvider.UpdateMovie(movie);
                    libraryProvider.DeleteAllMovieTags(movie.Id);
                    foreach (var tagId in tagIds)
                        libraryProvider.AddMovieTag(movie.Id, tagId);
                },
                updateProgress,
                cancel
            )
            .ConfigureAwait(false);
    }

    public bool MovieExists(string filename) => libraryProvider.MovieExists(filename);

    private async Task WithTransactionAsync(Action action, Action<double> updateProgress, CancellationToken cancel) =>
        await MutateAsync(() => libraryProvider.WithTransaction(action), updateProgress, cancel).ConfigureAwait(false);

    public async Task DeleteMoviesAsync(List<MovieId> movieIds, Action<double> updateProgress, CancellationToken cancel)
    {
        m3U8FolderSync.InvalidateAll();
        await MutateAsync(
                () =>
                {
                    foreach (var id in movieIds)
                        libraryProvider.DeleteMovie(id);
                },
                updateProgress,
                cancel
            )
            .ConfigureAwait(false);
    }

    public async Task RestoreMoviesAsync(
        List<MovieId> movieIds,
        Action<double> updateProgress,
        CancellationToken cancel
    )
    {
        await MutateAsync(
                () =>
                {
                    foreach (var id in movieIds)
                        libraryProvider.RestoreMovie(id);
                },
                updateProgress,
                cancel
            )
            .ConfigureAwait(false);
    }

    public async Task PermanentlyDeleteMoviesAsync(
        List<MovieId> movieIds,
        Action<double> updateProgress,
        CancellationToken cancel
    )
    {
        await MutateAsync(
                () =>
                {
                    foreach (var id in movieIds)
                        libraryProvider.PermanentlyDeleteMovie(id);
                },
                updateProgress,
                cancel
            )
            .ConfigureAwait(false);
    }

    public async Task SyncDownAsync(Action<double> updateProgress, CancellationToken cancel) =>
        await libraryProvider.SyncDownAsync(updateProgress, cancel).ConfigureAwait(false);
}
