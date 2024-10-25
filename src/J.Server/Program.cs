using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Web;
using Amazon.S3;
using J.Core;
using J.Core.Data;
using J.Server;
using Microsoft.AspNetCore.Mvc;

const int BLOCKS_PER_PAGE = 25;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCore();
builder.Services.AddHttpLogging(o => { });
builder.Services.AddTransient<ServerMovieFileReader>();
builder.Services.AddSingleton<IAmazonS3>(services =>
    services.GetRequiredService<AccountSettingsProvider>().CreateAmazonS3Client()
);

var app = builder.Build();
app.UseHttpLogging();

var libraryProvider = app.Services.GetRequiredService<LibraryProvider>();
libraryProvider.Connect();
var accountSettingsProvider = app.Services.GetRequiredService<AccountSettingsProvider>();
var accountSettings = accountSettingsProvider.Current;
var bucket = accountSettings.Bucket;
var password = accountSettings.Password ?? throw new Exception("Encryption key not found.");

object optionsLock = new();
var optionShuffle = true;
Filter optionFilter = new(true, []);
Dictionary<ListPageKey, Lazy<List<Page>>> listPages = [];
Dictionary<TagId, Lazy<List<Page>>> tagPages = [];

void RefreshLibrary()
{
    lock (optionsLock)
    {
        var movies = GetFilteredMovies(libraryProvider, optionFilter);

        // List pages
        {
            Dictionary<ListPageKey, Lazy<List<Page>>> dict = [];
            dict[new ListPageKey(ListPageType.Movies, null)] = new(
                () => SplitMoviesIntoPages(movies, "Movies", optionShuffle)
            );

            foreach (var tagType in libraryProvider.GetTagTypes())
            {
                ListPageKey key = new(ListPageType.TagType, tagType.Id);
                dict[key] = new(() => GetTagListPage(libraryProvider, tagType, optionShuffle));
            }

            listPages = dict;
        }

        // Individual tag pages
        {
            Dictionary<TagId, Lazy<List<Page>>> dict = [];
            var movieIds = movies.Select(x => x.Id).ToHashSet();
            foreach (var tag in libraryProvider.GetTags())
                dict[tag.Id] = new(
                    () =>
                        SplitMoviesIntoPages(
                            libraryProvider.GetMoviesWithTag(movieIds, tag.Id),
                            tag.Name,
                            optionShuffle
                        )
                );
            tagPages = dict;
        }
    }
}

static List<Movie> GetFilteredMovies(LibraryProvider libraryProvider, Filter filter)
{
    var movies = libraryProvider.GetMovies();
    if (filter.Rules.Count == 0)
        return movies;

    var movieTags = libraryProvider.GetMovieTags().ToLookup(x => x.MovieId, x => x.TagId);
    var tagTypes = libraryProvider.GetTags().ToDictionary(x => x.Id, x => x.TagTypeId);
    return movies.Where(x => IsMovieIncludedInFilter(x)).ToList();

    bool IsMovieIncludedInFilter(Movie movie)
    {
        var thisTagIds = movieTags[movie.Id];
        var thisTagTypes = thisTagIds.Select(x => tagTypes[x]);

        var anyTrue = false;
        var anyFalse = false;
        void Add(bool x)
        {
            if (x)
                anyTrue = true;
            else
                anyFalse = true;
        }

        foreach (var rule in filter.Rules)
        {
            switch (rule.Operator)
            {
                case FilterOperator.IsTagged:
                    Add(thisTagTypes.Contains(rule.Field.TagType!.Value.Id));
                    break;

                case FilterOperator.IsNotTagged:
                    Add(!thisTagTypes.Contains(rule.Field.TagType!.Value.Id));
                    break;

                case FilterOperator.IsTag:
                    foreach (var tag in rule.TagValues!)
                        Add(thisTagIds.Contains(tag.Id));
                    break;

                case FilterOperator.IsNotTag:
                    foreach (var tag in rule.TagValues!)
                        Add(!thisTagIds.Contains(tag.Id));
                    break;

                case FilterOperator.ContainsString:
                    foreach (var str in rule.StringValue!)
                        Add(movie.Filename.Contains(str, StringComparison.InvariantCultureIgnoreCase));
                    break;

                case FilterOperator.DoesNotContainString:
                    foreach (var str in rule.StringValue!)
                        Add(!movie.Filename.Contains(str, StringComparison.InvariantCultureIgnoreCase));
                    break;

                default:
                    throw new InvalidOperationException($"Unexpected operator: {rule.Operator}");
            }
        }

        if (filter.Or)
            return anyTrue; // OR
        else
            return !anyFalse; // AND
    }
}

static List<Page> GetTagListPage(LibraryProvider libraryProvider, TagType tagType, bool shuffle)
{
    var tags = libraryProvider.GetTags(tagType.Id);
    var dict = libraryProvider.GetRandomMoviePerTag(tagType);
    List<Page.Block> blocks = [];
    foreach (var tag in tags)
    {
        if (!dict.TryGetValue(tag.Id, out var movieId))
            continue;

        Page.Block block = new(movieId, tag.Id, tag.Name);
        blocks.Add(block);
    }
    return SplitBlocksIntoPages(blocks, tagType.PluralName, shuffle);
}

static List<Page> SplitMoviesIntoPages(List<Movie> movies, string title, bool shuffle)
{
    return SplitBlocksIntoPages(
        (from x in movies select new Page.Block(x.Id, null, x.Filename)).ToList(),
        title,
        shuffle
    );
}

static List<Page> SplitBlocksIntoPages(List<Page.Block> blocks, string title, bool shuffle)
{
    if (shuffle)
        Shuffle(blocks);
    else
        blocks.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.Ordinal));

    List<Page> pages = new(blocks.Count / BLOCKS_PER_PAGE + 1);
    var pageNumber = 1;
    foreach (var chunk in blocks.Chunk(BLOCKS_PER_PAGE))
    {
        Page page = new([.. chunk], "");
        pages.Add(page);
        pageNumber++;
    }

    for (var i = 0; i < pages.Count; i++)
    {
        pages[i] = pages[i] with { Title = $"{title} ({i + 1}/{pages.Count})" };
    }

    return pages;
}

static void Shuffle<T>(List<T> list)
{
    for (var i = 0; i < list.Count; i++)
    {
        var j = Random.Shared.Next(i, list.Count);
        (list[i], list[j]) = (list[j], list[i]);
    }
}

RefreshLibrary();

// ---

app.MapGet(
    "/movie.m3u8",
    async (
        [FromQuery, Required] string movieId,
        HttpResponse response,
        ServerMovieFileReader movieFileReader,
        CancellationToken cancel
    ) =>
    {
        var m3u8 = libraryProvider.GetM3u8(new(movieId));
        response.ContentType = "application/vnd.apple.mpegurl";
        await response.StartAsync(cancel);
        await response.Body.WriteAsync(m3u8, cancel);
    }
);

app.MapGet(
    "/movie.ts",
    async (
        [FromQuery, Required] string movieId,
        [FromQuery, Required] int index,
        HttpResponse response,
        ServerMovieFileReader movieFileReader,
        CancellationToken cancel
    ) =>
    {
        await using MemoryStream output = new();
        await ServerPolicy.Policy.ExecuteAsync(
            delegate
            {
                movieFileReader.ReadTs(new(movieId), index, bucket, password, output);
                return Task.CompletedTask;
            }
        );

        output.Position = 0;
        response.ContentType = "video/MP2T";
        await response.StartAsync(cancel);
        await output.CopyToAsync(response.Body, cancel);
    }
);

app.MapGet(
    "/clip.mp4",
    async ([FromQuery, Required] string movieId, HttpResponse response, CancellationToken cancel) =>
    {
        var bytes = libraryProvider.GetMovieClip(new(movieId));

        response.ContentType = "video/mp4";
        await response.StartAsync(cancel);
        await using var output = response.Body;
        await output.WriteAsync(bytes, cancel);
    }
);

app.MapPost("/refresh-library", RefreshLibrary);

app.MapGet(
    "/list.html",
    (
        [FromQuery, Required] string type,
        [FromQuery] string? tagTypeId,
        [FromQuery, Required] int pageIndex,
        HttpResponse response
    ) =>
    {
        Page page;

        var listPageType = (ListPageType)Enum.Parse(typeof(ListPageType), type);
        ListPageKey key = new(listPageType, tagTypeId is null ? null : new(tagTypeId));
        var lazy = listPages[key];
        if (pageIndex < 0 || pageIndex >= lazy.Value.Count)
            page = new([], "Blank");
        else
            page = lazy.Value[pageIndex];

        response.ContentType = "text/html";
        return page.ToHtml();
    }
);

app.MapGet(
    "/tag.html",
    ([FromQuery, Required] string tagId, [FromQuery, Required] int pageIndex, HttpResponse response) =>
    {
        Page page;

        var lazy = tagPages[new(tagId)];
        if (pageIndex < 0 || pageIndex >= lazy.Value.Count)
            page = new([], "");
        else
            page = lazy.Value[pageIndex];

        response.ContentType = "text/html";
        return page.ToHtml();
    }
);

app.MapPost(
    "/open-movie",
    ([FromQuery, Required] string movieId, HttpResponse response) =>
    {
        var movie = libraryProvider.GetMovie(new(movieId));
        var query = HttpUtility.ParseQueryString("");
        query["movieId"] = movie.Id.Value;
        var url = $"http://localhost:6786/movie.m3u8?{query}";
        var isSystemVlc = accountSettingsProvider.IsVlcInstalled.Value;
        ProcessStartInfo psi =
            new()
            {
                FileName =
                    isSystemVlc
                    ? "vlc.exe"
                    : Path.Combine(AppContext.BaseDirectory, "..", "vlc", "vlc.exe"),
                Arguments = url,
                UseShellExecute = isSystemVlc,
            };
        using var p = Process.Start(psi)!;
        ApplicationSubProcesses.Add(p);
    }
);

app.MapPost(
    "/shuffle",
    ([FromQuery, Required] bool on) =>
    {
        lock (optionsLock)
        {
            optionShuffle = on;
            RefreshLibrary();
        }
    }
);

app.MapPost(
    "/filter",
    ([FromBody] Filter filter) =>
    {
        lock (optionsLock)
        {
            optionFilter = filter;
            RefreshLibrary();
        }
    }
);

app.Run();

enum ListPageType
{
    Movies,
    TagType,
}

readonly record struct ListPageKey(ListPageType ListPageType, TagTypeId? TagTypeId);
