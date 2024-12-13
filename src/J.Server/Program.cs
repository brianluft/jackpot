using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Amazon.S3;
using J.Core;
using J.Core.Data;
using J.Server;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Console;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCore();
builder.Services.AddTransient<ServerMovieFileReader>();
builder.Services.AddSingleton<IAmazonS3>(services =>
    services.GetRequiredService<AccountSettingsProvider>().CreateAmazonS3Client()
);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<RequestLoggingMiddleware>();
builder.Services.AddScoped<RequestIdProvider>();
builder.Services.AddScoped<RequestLogger>();

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.FormatterName = "CustomConsole";
});
builder.Logging.Configure(options =>
{
    options.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId;
});
builder.Services.AddSingleton<ConsoleFormatter, CustomConsoleFormatter>();

var app = builder.Build();
app.UseMiddleware<RequestLoggingMiddleware>();

var configuredPort = GetPortNumber();
var configuredSessionPassword = Environment.GetEnvironmentVariable("JACKPOT_SESSION_PASSWORD") ?? "";
var libraryProvider = app.Services.GetRequiredService<LibraryProvider>();
libraryProvider.Connect();
var accountSettingsProvider = app.Services.GetRequiredService<AccountSettingsProvider>();
var accountSettings = accountSettingsProvider.Current;
var bucket = accountSettings.Bucket;
var password = accountSettings.Password ?? throw new Exception("Encryption key not found.");

var preferences = app.Services.GetRequiredService<Preferences>();
var seed = Guid.NewGuid().GetHashCode();

// The app will set this flag when it wants the next page load NOT to restore the scroll position.
Lock inhibitScrollRestoreLock = new();
var inhibitScrollRestore = false;
void SetInhibitScrollRestore()
{
    lock (inhibitScrollRestoreLock)
    {
        inhibitScrollRestore = true;
    }
}
bool TakeInhibitScrollRestore()
{
    lock (inhibitScrollRestoreLock)
    {
        if (inhibitScrollRestore)
        {
            inhibitScrollRestore = false;
            return true;
        }
        return false;
    }
}

var staticFiles = Directory
    .GetFiles(Path.Combine(AppContext.BaseDirectory, "static"))
    .ToDictionary(x => Path.GetFileName(x), File.ReadAllBytes);

List<Movie> GetFilteredMovies(LibraryMetadata libraryMetadata)
{
    var filter = preferences.GetJson<Filter>(Preferences.Key.Shared_Filter);
    var movies = libraryMetadata.Movies.Values;
    var movieTags = libraryMetadata.MovieTags;
    var tagTypes = libraryMetadata.TagTypes;
    var tags = libraryMetadata.Tags;
    var searchTerms = WhitespaceRegex().Split(filter.Search);
    return movies.Where(IsMovieIncludedInFilter).ToList();

    bool IsMovieIncludedInFilter(Movie movie)
    {
        var thisTagIds = movieTags[movie.Id];
        var thisTagTypeIds = thisTagIds.Select(x => tagTypes[tags[x].TagTypeId].Id);

        var anyTrue = false;
        var anyFalse = false;
        void Add(bool x)
        {
            if (x)
                anyTrue = true;
            else
                anyFalse = true;
        }

        foreach (var searchTerm in searchTerms)
        {
            if (searchTerm.Length == 0)
                continue;

            Add(movie.Filename.Contains(searchTerm, StringComparison.CurrentCultureIgnoreCase));
        }

        foreach (var rule in filter.Rules)
        {
            switch (rule.Operator)
            {
                case FilterOperator.IsTagged:
                    Add(thisTagTypeIds.Contains(rule.Field.TagTypeId!));
                    break;

                case FilterOperator.IsNotTagged:
                    Add(!thisTagTypeIds.Contains(rule.Field.TagTypeId!));
                    break;

                case FilterOperator.IsTag:
                    foreach (var tagId in rule.TagValues!)
                        Add(thisTagIds.Contains(tagId));
                    break;

                case FilterOperator.IsNotTag:
                    foreach (var tagId in rule.TagValues!)
                        Add(!thisTagIds.Contains(tagId));
                    break;

                case FilterOperator.ContainsString:
                    Add(movie.Filename.Contains(rule.StringValue!, StringComparison.InvariantCultureIgnoreCase));
                    break;

                case FilterOperator.DoesNotContainString:
                    Add(!movie.Filename.Contains(rule.StringValue!, StringComparison.InvariantCultureIgnoreCase));
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

Html GetTagListPage(
    LibraryProvider libraryProvider,
    TagType tagType,
    SortOrder sortOrder,
    LibraryMetadata libraryMetadata,
    string filterSortHash
)
{
    var tags = libraryProvider.GetTags(tagType.Id);
    var dict = libraryProvider.GetRandomMoviePerTag(tagType);
    List<PageBlock> blocks = [];
    foreach (var tag in tags)
    {
        if (!dict.TryGetValue(tag.Id, out var movieId))
            continue;

        PageBlock block = new(movieId, tag.Id, null, tag.Name, DateTimeOffset.Now, [], []);
        blocks.Add(block);
    }

    // One final block for any movie that doesn't have any tags of this type.
    Movie? untaggedMovie = null;
    foreach (var movie in libraryMetadata.Movies.Values)
    {
        if (!libraryMetadata.MovieTags[movie.Id].Any(x => libraryMetadata.Tags[x].TagTypeId == tagType.Id))
        {
            untaggedMovie = movie;
            break;
        }
    }
    if (untaggedMovie is not null)
    {
        PageBlock block =
            new(untaggedMovie.Value.Id, null, tagType.Id, $"(No {tagType.SingularName})", DateTimeOffset.Now, [], []);
        blocks.Add(block);
    }

    return NewPageFromBlocks(
        blocks,
        [],
        sortOrder,
        tagType.PluralName,
        $"TagListPage_{tagType.Id.Value}",
        filterSortHash
    );
}

static string GetField(PageBlock block, string field)
{
    if (field == "name")
        return block.Title;
    if (field == "date")
        return block.Date.UtcDateTime.ToString("u");

    TagTypeId tagTypeId = new(field);
    if (block.SortTags.TryGetValue(tagTypeId, out var value))
        return value;

    return "";
}

Html NewPageFromBlocks(
    List<PageBlock> blocks,
    List<string> metadataKeys,
    SortOrder sortOrder,
    string title,
    string cookieName,
    string filterSortHash
)
{
    if (blocks.Count == 0)
    {
        var message = libraryProvider.HasMovies()
            ? "No movies match the filter."
            : "Click &ldquo;Import&rdquo; above to get started.";
        return EmptyPage.GenerateHtml(title, message);
    }

    if (sortOrder.Shuffle)
    {
        Shuffle(blocks);
    }
    else
    {
        blocks.Sort(
            (a, b) =>
            {
                var valueA = GetField(a, sortOrder.Field);
                var valueB = GetField(b, sortOrder.Field);

                // Sort empty string to the end
                if (valueA == "" && valueB != "")
                    return 1;
                if (valueA != "" && valueB == "")
                    return -1;

                var x = string.Compare(valueA, valueB, StringComparison.CurrentCulture);
                if (x != 0)
                    return x;

                x = string.Compare(a.Title, b.Title, StringComparison.CurrentCulture);
                if (x != 0)
                    return x;

                return string.Compare(a.MovieId.Value, b.MovieId.Value, StringComparison.Ordinal);
            }
        );

        if (!sortOrder.Ascending)
            blocks.Reverse();
    }

    var style = preferences.GetEnum<LibraryViewStyle>(Preferences.Key.Shared_LibraryViewStyle);
    var columnCount = (int)preferences.GetInteger(Preferences.Key.Shared_ColumnCount);
    var thisInhibitScrollRestore = TakeInhibitScrollRestore();
    return style switch
    {
        LibraryViewStyle.List => ListPage.GenerateHtml(
            blocks,
            metadataKeys,
            title,
            configuredSessionPassword,
            cookieName,
            filterSortHash,
            thisInhibitScrollRestore
        ),
        LibraryViewStyle.Grid => WallPage.GenerateHtml(
            blocks,
            title,
            configuredSessionPassword,
            columnCount,
            cookieName,
            filterSortHash,
            thisInhibitScrollRestore
        ),
        _ => throw new Exception($"Unexpected LibraryViewStyle: {style}"),
    };
}

static Dictionary<TagTypeId, string> GetSortTags(Movie movie, LibraryMetadata libraryMetadata)
{
    // Get the alphabetically first tag of each type
    return libraryMetadata
        .MovieTags[movie.Id]
        .Select(x => libraryMetadata.Tags[x])
        .GroupBy(x => x.TagTypeId)
        .ToDictionary(x => x.Key, x => x.Min(x => x.Name)!);
}

Html NewPageFromMovies(
    IEnumerable<Movie> movies,
    LibraryMetadata libraryMetadata,
    SortOrder sortOrder,
    string title,
    string cookieName,
    string filterSortHash
)
{
    var metadataKeys = libraryMetadata
        .TagTypes.Values.OrderBy(x => x.SortIndex)
        .ThenBy(x => x.SingularName)
        .Select(x => x.SingularName)
        .ToList();
    metadataKeys.Remove("Date Added");
    metadataKeys.Add("Date Added");

    return NewPageFromBlocks(
        (
            from x in movies
            select new PageBlock(
                x.Id,
                null,
                null,
                x.Filename,
                x.DateAdded,
                GetSortTags(x, libraryMetadata),
                libraryMetadata.GetMovieMetadata(x.Id)
            )
        ).ToList(),
        metadataKeys,
        sortOrder,
        title,
        cookieName,
        filterSortHash
    );
}

void Shuffle<T>(List<T> list)
{
    Random random = new(seed);

    for (var i = 0; i < list.Count; i++)
    {
        var j = random.Next(i, list.Count);
        (list[i], list[j]) = (list[j], list[i]);
    }
}

static int GetPortNumber()
{
    var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
    if (string.IsNullOrEmpty(urls))
    {
        throw new InvalidOperationException("ASPNETCORE_URLS environment variable is not set");
    }

    // Take first URL if multiple are specified
    var url = urls.Split(';').First();

    // Find the last occurrence of : and parse everything after it
    var lastColon = url.LastIndexOf(':');
    if (lastColon == -1)
    {
        throw new InvalidOperationException($"Could not parse port from URL: {url}");
    }

    var portStr = url[(lastColon + 1)..];
    if (!int.TryParse(portStr, out int port))
    {
        throw new InvalidOperationException($"Invalid port number: {portStr}");
    }

    return port;
}

void CheckSessionPassword(string sessionPassword)
{
    if (sessionPassword != configuredSessionPassword)
        throw new Exception("Unrecognized caller.");
}

LibraryMetadata GetLibraryMetadata()
{
    return new(
        libraryProvider.GetMovies().Where(x => !x.Deleted).ToDictionary(x => x.Id),
        libraryProvider.GetMovieTags().ToLookup(x => x.MovieId, x => x.TagId),
        libraryProvider.GetTags().ToDictionary(x => x.Id),
        libraryProvider.GetTagTypes().ToDictionary(x => x.Id)
    );
}

string GetFilterSortHash()
{
    var filter = preferences.GetText(Preferences.Key.Shared_Filter);
    var sortOrder = preferences.GetText(Preferences.Key.Shared_SortOrder);

    var filterHash = SHA256.HashData(Encoding.UTF8.GetBytes(filter));
    var sortOrderHash = SHA256.HashData(Encoding.UTF8.GetBytes(sortOrder));

    return BitConverter.ToString([.. filterHash, .. sortOrderHash]);
}

// ---

app.MapGet(
    "/favicon.ico",
    async (HttpResponse response, CancellationToken cancel) =>
    {
        response.ContentType = "image/x-icon";
        await response.StartAsync(cancel);
        await response.Body.WriteAsync(staticFiles["favicon.ico"], cancel);
    }
);

app.MapGet(
    "/static/{filename}",
    async ([FromRoute] string filename, HttpResponse response, CancellationToken cancel) =>
    {
        if (!staticFiles.TryGetValue(filename, out var bytes))
        {
            response.StatusCode = 404;
            return;
        }

        var ext = Path.GetExtension(filename);
        response.ContentType = ext switch
        {
            ".css" => "text/css",
            ".js" => "text/javascript",
            _ => "application/octet-stream",
        };

        await response.StartAsync(cancel);
        await response.Body.WriteAsync(bytes, cancel);
    }
);

app.MapGet(
    "/movie.m3u8",
    async (
        [FromQuery, Required] string movieId,
        [FromQuery, Required] string sessionPassword,
        HttpResponse response,
        ServerMovieFileReader movieFileReader,
        CancellationToken cancel
    ) =>
    {
        CheckSessionPassword(sessionPassword);
        var m3u8 = libraryProvider.GetM3u8(new(movieId), configuredPort, configuredSessionPassword, "localhost");
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
        [FromQuery, Required] string sessionPassword,
        HttpResponse response,
        ServerMovieFileReader movieFileReader,
        RequestLogger logger,
        CancellationToken cancel
    ) =>
    {
        CheckSessionPassword(sessionPassword);

        var sw = Stopwatch.StartNew();

        await using MemoryStream output = new();
        logger.Log($"Requesting from S3.");
        await ServerPolicy.Policy.ExecuteAsync(
            delegate
            {
                movieFileReader.ReadTs(new(movieId), index, bucket, password, output);
                return Task.CompletedTask;
            }
        );

        logger.Log($"Read {output.Length:#,##0} bytes from S3 in {sw.Elapsed.TotalMilliseconds:#,##0} ms.");

        sw.Restart();

        output.Position = 0;
        response.ContentType = "video/MP2T";
        await response.StartAsync(cancel);
        await output.CopyToAsync(response.Body, cancel);

        logger.Log($"Sent response in {sw.Elapsed.TotalMilliseconds:#,##0} ms.");
    }
);

app.MapGet(
    "/clip.mp4",
    async (
        [FromQuery, Required] string movieId,
        [FromQuery, Required] string sessionPassword,
        HttpResponse response,
        CancellationToken cancel
    ) =>
    {
        CheckSessionPassword(sessionPassword);
        var bytes = libraryProvider.GetMovieClip(new(movieId));

        response.ContentType = "video/mp4";
        await response.StartAsync(cancel);
        await using var output = response.Body;
        await output.WriteAsync(bytes, cancel);
    }
);

app.MapGet(
    "/list.html",
    (
        [FromQuery, Required] string type,
        [FromQuery] string? tagTypeId,
        [FromQuery, Required] string sessionPassword,
        HttpResponse response
    ) =>
    {
        CheckSessionPassword(sessionPassword);

        var listPageType = Enum.Parse<ListPageType>(type);
        var sortOrder = preferences.GetJson<SortOrder>(Preferences.Key.Shared_SortOrder);
        var libraryMetadata = GetLibraryMetadata();

        Html html;
        var filterSortHash = GetFilterSortHash();
        if (listPageType == ListPageType.Movies)
        {
            var movies = GetFilteredMovies(libraryMetadata);
            html = NewPageFromMovies(movies, libraryMetadata, sortOrder, "Movies", "movies", filterSortHash);
        }
        else if (listPageType == ListPageType.TagType)
        {
            var tagType = libraryProvider.GetTagType(new(tagTypeId!));
            html = GetTagListPage(libraryProvider, tagType, sortOrder, libraryMetadata, filterSortHash);
        }
        else
        {
            throw new Exception($"Unexpected ListPageType: {listPageType}");
        }

        response.ContentType = "text/html";
        return html.Content;
    }
);

app.MapGet(
    "/tag.html",
    ([FromQuery, Required] string tagId, [FromQuery, Required] string sessionPassword, HttpResponse response) =>
    {
        CheckSessionPassword(sessionPassword);

        var sortOrder = preferences.GetJson<SortOrder>(Preferences.Key.Shared_SortOrder);
        var libraryMetadata = GetLibraryMetadata();
        var movies = GetFilteredMovies(libraryMetadata).ToDictionary(x => x.Id);
        var tag = libraryProvider.GetTag(new(tagId));

        List<Movie> taggedMovies = [];
        foreach (var movieId in libraryProvider.GetMovieIdsWithTag(tag.Id))
        {
            if (movies.TryGetValue(movieId, out var movie))
                taggedMovies.Add(movie);
        }

        var html = NewPageFromMovies(
            taggedMovies,
            libraryMetadata,
            sortOrder,
            tag.Name,
            $"tag_{tag.Id.Value}",
            GetFilterSortHash()
        );

        response.ContentType = "text/html";
        return html.Content;
    }
);

app.MapGet(
    "/untagged.html",
    ([FromQuery, Required] string tagTypeId, [FromQuery, Required] string sessionPassword, HttpResponse response) =>
    {
        CheckSessionPassword(sessionPassword);

        var sortOrder = preferences.GetJson<SortOrder>(Preferences.Key.Shared_SortOrder);
        var libraryMetadata = GetLibraryMetadata();
        var movies = GetFilteredMovies(libraryMetadata).ToDictionary(x => x.Id);
        var tagType = libraryProvider.GetTagType(new(tagTypeId));

        List<Movie> untaggedMovies = [];
        foreach (var movieId in libraryProvider.GetMovieIdsWithoutTagType(tagType.Id))
        {
            if (movies.TryGetValue(movieId, out var movie))
                untaggedMovies.Add(movie);
        }

        var html = NewPageFromMovies(
            untaggedMovies,
            libraryMetadata,
            sortOrder,
            $"No {tagType.SingularName}",
            $"untagged_{tagType.Id.Value}",
            GetFilterSortHash()
        );

        response.ContentType = "text/html";
        return html.Content;
    }
);

app.MapGet(
    "/movie-preview.html",
    ([FromQuery, Required] string movieId, [FromQuery, Required] string sessionPassword, HttpResponse response) =>
    {
        CheckSessionPassword(sessionPassword);

        response.ContentType = "text/html";
        return MoviePreviewPage.GenerateHtml(new(movieId), sessionPassword, "").Content;
    }
);

app.MapGet(
    "/movie-play.html",
    ([FromQuery, Required] string movieId, [FromQuery, Required] string sessionPassword, HttpResponse response) =>
    {
        CheckSessionPassword(sessionPassword);

        var m3u8Url = $"/movie.m3u8?movieId={movieId}&sessionPassword={sessionPassword}";
        var movie = libraryProvider.GetMovie(new(movieId));

        response.ContentType = "text/html";
        return MoviePlayerPage.GenerateHtml(movie.Filename, m3u8Url).Content;
    }
);

app.MapPost(
    "/reshuffle",
    ([FromQuery, Required] string sessionPassword) =>
    {
        CheckSessionPassword(sessionPassword);
        seed = Guid.NewGuid().GetHashCode();
    }
);

app.MapPost(
    "/inhibit-scroll-restore",
    ([FromQuery, Required] string sessionPassword) =>
    {
        CheckSessionPassword(sessionPassword);
        SetInhibitScrollRestore();
    }
);

if (preferences.GetBoolean(Preferences.Key.NetworkSharing_AllowWebBrowserAccess))
{
    app.MapGet(
        "/",
        (HttpResponse response) =>
        {
            var tagTypes = libraryProvider.GetTagTypes();
            var html = BrowserIndexPage.GenerateHtml(tagTypes);
            response.ContentType = "text/html";
            return html.Content;
        }
    );

    app.MapGet(
        "/browser-movies.html",
        (HttpResponse response) =>
        {
            var movies = libraryProvider.GetMovies().Where(x => !x.Deleted).ToList();
            var html = BrowserMoviesPage.GenerateHtml(movies, configuredSessionPassword);
            response.ContentType = "text/html";
            return html.Content;
        }
    );

    app.MapGet(
        "/browser-tagtype.html",
        ([FromQuery, Required] string tagTypeId, HttpResponse response) =>
        {
            var tagType = libraryProvider.GetTagType(new(tagTypeId));
            var tags = libraryProvider.GetTags(tagType.Id);
            var html = BrowserTagTypePage.GenerateHtml(tagType, tags);
            response.ContentType = "text/html";
            return html.Content;
        }
    );

    app.MapGet(
        "/browser-tag.html",
        ([FromQuery, Required] string tagTypeId, [FromQuery, Required] string tagId, HttpResponse response) =>
        {
            var tag = libraryProvider.GetTag(new(tagId));
            var movieIds = libraryProvider.GetMovieIdsWithTag(tag.Id).ToHashSet();
            var movies = libraryProvider.GetMovies().Where(x => movieIds.Contains(x.Id)).ToList();
            var html = BrowserTagPage.GenerateHtml(new(tagTypeId), tag, movies, configuredSessionPassword);
            response.ContentType = "text/html";
            return html.Content;
        }
    );
}

app.Run();

enum ListPageType
{
    Movies,
    TagType,
}

record class LibraryMetadata(
    Dictionary<MovieId, Movie> Movies,
    ILookup<MovieId, TagId> MovieTags,
    Dictionary<TagId, Tag> Tags,
    Dictionary<TagTypeId, TagType> TagTypes
)
{
    public Dictionary<string, string> GetMovieMetadata(MovieId movieId)
    {
        var dict = MovieTags[movieId]
            .Select(tagId => Tags[tagId])
            .GroupBy(tag => TagTypes[tag.TagTypeId].SingularName)
            .ToDictionary(
                group => group.Key,
                group => string.Join(" &nbsp;&bull;&nbsp; ", group.Select(tag => tag.Name).OrderBy(name => name))
            );

        dict["Date Added"] = Movies[movieId].DateAdded.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        return dict;
    }
}

public partial class Program
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
