﻿using J.Core;
using J.Core.Data;

namespace J.App;

public sealed class LibraryProviderAdapter(
    LibraryProvider libraryProvider,
    Client client,
    M3u8FolderSync m3U8FolderSync
)
{
    public void Connect() => libraryProvider.Connect();

    public void Disconnect() => libraryProvider.Disconnect();

    private async Task MutateAsync(Action action, CancellationToken cancel)
    {
        await libraryProvider.SyncDownAsync(cancel).ConfigureAwait(false);
        action();
        await libraryProvider.SyncUpAsync(cancel).ConfigureAwait(false);
        await client.RefreshLibraryAsync(cancel).ConfigureAwait(false);
        m3U8FolderSync.Sync();
    }

    public async Task NewTagAsync(Tag tag, CancellationToken cancel) =>
        await MutateAsync(() => libraryProvider.NewTag(tag), cancel).ConfigureAwait(false);

    public async Task UpdateTagAsync(Tag tag, CancellationToken cancel) =>
        await MutateAsync(() => libraryProvider.UpdateTag(tag), cancel).ConfigureAwait(false);

    public Tag GetTag(TagId id) => libraryProvider.GetTag(id);

    public async Task DeleteTagAsync(TagId id, CancellationToken cancel) =>
        await MutateAsync(() => libraryProvider.DeleteTag(id), cancel).ConfigureAwait(false);

    public List<Movie> GetMovies() => libraryProvider.GetMovies();

    public Movie GetMovie(MovieId id) => libraryProvider.GetMovie(id);

    public byte[] GetMovieClip(MovieId movieId) => libraryProvider.GetMovieClip(movieId);

    public byte[] GetM3u8(MovieId movieId) => libraryProvider.GetM3u8(movieId);

    public async Task NewTagTypeAsync(TagType tagType, CancellationToken cancel) =>
        await MutateAsync(() => libraryProvider.NewTagType(tagType), cancel).ConfigureAwait(false);

    public List<TagType> GetTagTypes() => libraryProvider.GetTagTypes();

    public List<Tag> GetTags() => libraryProvider.GetTags();

    public List<Tag> GetTags(TagTypeId tagTypeId) => libraryProvider.GetTags(tagTypeId);

    public List<MovieTag> GetMovieTags() => libraryProvider.GetMovieTags();

    public List<MovieTag> GetMovieTags(MovieId movieId) => libraryProvider.GetMovieTags(movieId);

    public async Task AddMovieTagsAsync(List<(MovieId MovieId, TagId TagId)> list, CancellationToken cancel) =>
        await WithTransactionAsync(
                () =>
                {
                    foreach (var (movieId, tagId) in list)
                        libraryProvider.AddMovieTag(movieId, tagId);
                },
                cancel
            )
            .ConfigureAwait(false);

    public async Task DeleteMovieTagsAsync(List<MovieTag> list, CancellationToken cancel) =>
        await WithTransactionAsync(
                () =>
                {
                    foreach (var (movieId, tagId) in list)
                        libraryProvider.DeleteMovieTag(movieId, tagId);
                },
                cancel
            )
            .ConfigureAwait(false);

    public async Task NewMovieAsync(Movie movie, List<MovieFile> files, CancellationToken cancel) =>
        await WithTransactionAsync(
                () =>
                {
                    libraryProvider.NewMovie(movie);
                    foreach (var file in files)
                        libraryProvider.NewMovieFile(file);
                },
                cancel
            )
            .ConfigureAwait(false);

    public async Task UpdateMovieAsync(Movie movie, CancellationToken cancel) =>
        await MutateAsync(() => libraryProvider.UpdateMovie(movie), cancel).ConfigureAwait(false);

    private async Task WithTransactionAsync(Action action, CancellationToken cancel) =>
        await MutateAsync(() => libraryProvider.WithTransaction(action), cancel).ConfigureAwait(false);

    public async Task DeleteMovieAsync(MovieId id, CancellationToken cancel) =>
        await MutateAsync(() => libraryProvider.DeleteMovie(id), cancel).ConfigureAwait(false);

    public async Task SyncDownAsync(CancellationToken cancel) =>
        await libraryProvider.SyncDownAsync(cancel).ConfigureAwait(false);
}