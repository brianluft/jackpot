using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Web;
using Amazon.S3;
using J.Base;
using J.Core;
using J.Core.Data;
using J.Server;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Win32;

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
Dictionary<ListPageKey, Lazy<Page>> listPages = [];
Dictionary<TagId, Lazy<Page>> tagPages = [];

void RefreshLibrary()
{
    var sortOrder = preferences.GetJson<SortOrder>(Preferences.Key.Shared_SortOrder);
    var filter = preferences.GetJson<Filter>(Preferences.Key.Shared_Filter);

    LibraryMetadata libraryMetadata =
        new(
            libraryProvider.GetMovies().ToDictionary(x => x.Id),
            libraryProvider.GetMovieTags().ToLookup(x => x.MovieId, x => x.TagId),
            libraryProvider.GetTags().ToDictionary(x => x.Id)
        );

    var movies = GetFilteredMovies(libraryProvider, filter);

    // List pages
    {
        Dictionary<ListPageKey, Lazy<Page>> dict = [];
        dict[new ListPageKey(ListPageType.Movies, null)] = new(
            () => NewPageFromMovies(movies, libraryMetadata, sortOrder, "Movies")
        );

        foreach (var tagType in libraryProvider.GetTagTypes())
        {
            ListPageKey key = new(ListPageType.TagType, tagType.Id);
            dict[key] = new(() => GetTagListPage(libraryProvider, tagType, sortOrder, libraryMetadata));
        }

        listPages = dict;
    }

    // Individual tag pages
    {
        Dictionary<TagId, Lazy<Page>> dict = [];
        var movieIds = movies.Select(x => x.Id).ToHashSet();
        foreach (var tag in libraryProvider.GetTags())
            dict[tag.Id] = new(
                () =>
                    NewPageFromMovies(
                        libraryProvider.GetMoviesWithTag(movieIds, tag.Id),
                        libraryMetadata,
                        sortOrder,
                        tag.Name
                    )
            );
        tagPages = dict;
    }
}

static List<Movie> GetFilteredMovies(LibraryProvider libraryProvider, Filter filter)
{
    var movies = libraryProvider.GetMovies();
    if (filter.Rules.Count == 0)
        return movies;

    var movieTags = libraryProvider.GetMovieTags().ToLookup(x => x.MovieId, x => x.TagId);
    var tagTypes = libraryProvider.GetTags().ToDictionary(x => x.Id, x => x.TagTypeId);
    return movies.Where(IsMovieIncludedInFilter).ToList();

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
                    Add(thisTagTypes.Contains(rule.Field.TagTypeId!));
                    break;

                case FilterOperator.IsNotTagged:
                    Add(!thisTagTypes.Contains(rule.Field.TagTypeId!));
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

static Page GetTagListPage(
    LibraryProvider libraryProvider,
    TagType tagType,
    SortOrder sortOrder,
    LibraryMetadata libraryMetadata
)
{
    var tags = libraryProvider.GetTags(tagType.Id);
    var dict = libraryProvider.GetRandomMoviePerTag(tagType);
    List<Page.Block> blocks = [];
    foreach (var tag in tags)
    {
        if (!dict.TryGetValue(tag.Id, out var movieId))
            continue;

        Page.Block block = new(movieId, tag.Id, tag.Name, []);
        blocks.Add(block);
    }
    return NewPageFromBlocks(blocks, sortOrder, tagType.PluralName);
}

static string GetField(Page.Block block, string field)
{
    if (field == "name")
        return block.Title;

    TagTypeId tagTypeId = new(field);
    if (block.SortTags.TryGetValue(tagTypeId, out var value))
        return value;

    return "";
}

static Page NewPageFromBlocks(List<Page.Block> blocks, SortOrder sortOrder, string title)
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

    return new(blocks, title);
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

static Page NewPageFromMovies(
    IEnumerable<Movie> movies,
    LibraryMetadata libraryMetadata,
    SortOrder sortOrder,
    string title
)
{
    return NewPageFromBlocks(
        (from x in movies select new Page.Block(x.Id, null, x.Filename, GetSortTags(x, libraryMetadata))).ToList(),
        sortOrder,
        title
    );
}

static void Shuffle<T>(List<T> list)
{
    for (var i = 0; i < list.Count; i++)
    {
        var j = Random.Shared.Next(i, list.Count);
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

static bool IsVlcInstalled()
{
    try
    {
        using var key = Registry.ClassesRoot.OpenSubKey(@"Applications\vlc.exe");
        return key is not null;
    }
    catch
    {
        return false;
    }
}

RefreshLibrary();

// ---

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
        var m3u8 = libraryProvider.GetM3u8(new(movieId), configuredPort, configuredSessionPassword);
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

app.MapPost(
    "/refresh-library",
    ([FromQuery, Required] string sessionPassword) =>
    {
        CheckSessionPassword(sessionPassword);
        RefreshLibrary();
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

        var listPageType = (ListPageType)Enum.Parse(typeof(ListPageType), type);
        ListPageKey key = new(listPageType, tagTypeId is null ? null : new(tagTypeId));

        response.ContentType = "text/html";
        var columnCount = (int)preferences.GetInteger(Preferences.Key.Shared_ColumnCount);
        return listPages[key].Value.ToHtml(configuredSessionPassword, columnCount);
    }
);

app.MapGet(
    "/tag.html",
    ([FromQuery, Required] string tagId, [FromQuery, Required] string sessionPassword, HttpResponse response) =>
    {
        CheckSessionPassword(sessionPassword);

        response.ContentType = "text/html";
        var columnCount = (int)preferences.GetInteger(Preferences.Key.Shared_ColumnCount);
        return tagPages[new(tagId)].Value.ToHtml(configuredSessionPassword, columnCount);
    }
);

app.MapPost(
    "/open-movie",
    ([FromQuery, Required] string movieId, [FromQuery, Required] string sessionPassword, HttpResponse response) =>
    {
        CheckSessionPassword(sessionPassword);
        var movie = libraryProvider.GetMovie(new(movieId));
        var query = HttpUtility.ParseQueryString("");
        query["movieId"] = movie.Id.Value;
        query["sessionPassword"] = configuredSessionPassword;
        var url = $"http://localhost:{configuredPort}/movie.m3u8?{query}";

        var which = preferences.GetEnum<VlcInstallationToUse>(Preferences.Key.Shared_VlcInstallationToUse);
        if (which == VlcInstallationToUse.Automatic)
            which = IsVlcInstalled() ? VlcInstallationToUse.System : VlcInstallationToUse.Bundled;

        var extraArgs = "";
        if (which == VlcInstallationToUse.Bundled)
        {
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Jackpot",
                "vlcrc"
            );

            if (!File.Exists(configPath))
            {
                File.WriteAllText(
                    configPath,
                    """
                    metadata-network-access=0
                    qt-updates-notif=0
                    qt-privacy-ask=0
                    """
                );
            }

            extraArgs = $"--config \"{configPath}\"";
        }

        ProcessStartInfo psi =
            new()
            {
                FileName =
                    which == VlcInstallationToUse.System
                        ? "vlc.exe"
                        : Path.Combine(AppContext.BaseDirectory, "..", "vlc", "vlc.exe"),
                Arguments = $"--fullscreen --high-priority {extraArgs} -- {url}",
                UseShellExecute = which == VlcInstallationToUse.System,
            };
        using var p = Process.Start(psi)!;
        ApplicationSubProcesses.Add(p);
    }
);

app.Run();

enum ListPageType
{
    Movies,
    TagType,
}

readonly record struct ListPageKey(ListPageType ListPageType, TagTypeId? TagTypeId);

readonly record struct LibraryMetadata(
    Dictionary<MovieId, Movie> Movies,
    ILookup<MovieId, TagId> MovieTags,
    Dictionary<TagId, Tag> Tags
);
