using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Amazon.S3;
using J.Core;
using J.Core.Data;
using J.Server;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCore();
builder.Services.AddHttpLogging(o => { });
builder.Services.AddTransient<ServerMovieFileReader>();
builder.Services.AddSingleton<IAmazonS3>(services =>
    services.GetRequiredService<AccountSettingsProvider>().CreateAmazonS3Client()
);

var app = builder.Build();
app.UseHttpLogging();

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

var staticFiles = Directory
    .GetFiles(Path.Combine(AppContext.BaseDirectory, "static"))
    .ToDictionary(x => Path.GetFileName(x), File.ReadAllBytes);

string GetM3u8Hostname()
{
    var m3u8Hostname = preferences.GetJson<M3u8SyncSettings>(Preferences.Key.M3u8FolderSync_Settings).M3u8Hostname;
    if (string.IsNullOrWhiteSpace(m3u8Hostname))
        m3u8Hostname = "localhost";

    return m3u8Hostname;
}

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
    LibraryMetadata libraryMetadata
)
{
    var tags = libraryProvider.GetTags(tagType.Id);
    var dict = libraryProvider.GetRandomMoviePerTag(tagType);
    List<PageBlock> blocks = [];
    foreach (var tag in tags)
    {
        if (!dict.TryGetValue(tag.Id, out var movieId))
            continue;

        PageBlock block = new(movieId, tag.Id, tag.Name, DateTimeOffset.Now, [], []);
        blocks.Add(block);
    }
    return NewPageFromBlocks(blocks, [], sortOrder, tagType.PluralName, $"TagListPage_{tagType.Id.Value}");
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
    string cookieName
)
{
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
    return style switch
    {
        LibraryViewStyle.List => ListPage.GenerateHtml(
            blocks,
            metadataKeys,
            title,
            configuredSessionPassword,
            cookieName
        ),
        LibraryViewStyle.Grid => WallPage.GenerateHtml(
            blocks,
            title,
            configuredSessionPassword,
            columnCount,
            cookieName
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
    string cookieName
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
                x.Filename,
                x.DateAdded,
                GetSortTags(x, libraryMetadata),
                libraryMetadata.GetMovieMetadata(x.Id)
            )
        ).ToList(),
        metadataKeys,
        sortOrder,
        title,
        cookieName
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
        libraryProvider.GetMovies().ToDictionary(x => x.Id),
        libraryProvider.GetMovieTags().ToLookup(x => x.MovieId, x => x.TagId),
        libraryProvider.GetTags().ToDictionary(x => x.Id),
        libraryProvider.GetTagTypes().ToDictionary(x => x.Id)
    );
}

// ---

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
        var m3u8 = libraryProvider.GetM3u8(new(movieId), configuredPort, configuredSessionPassword, GetM3u8Hostname());
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
        CancellationToken cancel
    ) =>
    {
        CheckSessionPassword(sessionPassword);
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
        if (listPageType == ListPageType.Movies)
        {
            var movies = GetFilteredMovies(libraryMetadata);
            html = NewPageFromMovies(movies, libraryMetadata, sortOrder, "Movies", "movies");
        }
        else if (listPageType == ListPageType.TagType)
        {
            var tagType = libraryProvider.GetTagType(new(tagTypeId!));
            html = GetTagListPage(libraryProvider, tagType, sortOrder, libraryMetadata);
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
        var movies = GetFilteredMovies(libraryMetadata);
        var movieIds = movies.Select(x => x.Id).ToHashSet();
        var tag = libraryProvider.GetTag(new(tagId));

        var html = NewPageFromMovies(
            libraryProvider.GetMoviesWithTag(movieIds, tag.Id),
            libraryMetadata,
            sortOrder,
            tag.Name,
            $"tag_{tag.Id.Value}"
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
        return MoviePreviewPage.GenerateHtml(new(movieId), sessionPassword).Content;
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

app.Run();

enum ListPageType
{
    Movies,
    TagType,
}

readonly record struct ListPageKey(ListPageType ListPageType, TagTypeId? TagTypeId);

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
