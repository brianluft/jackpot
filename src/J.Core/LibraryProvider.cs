using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Amazon.S3;
using J.Core.Data;
using Microsoft.Data.Sqlite;
using Polly;
using Polly.Timeout;
using Polly.Wrap;

namespace J.Core;

public sealed partial class LibraryProvider : IDisposable
{
    public const int CURRENT_VERSION = 1;

    private readonly AccountSettingsProvider _accountSettingsProvider;
    private readonly ProcessTempDir _processTempDir;

    private readonly AsyncPolicyWrap _policy = Policy.WrapAsync(
        // Outer: retry
        Policy
            .Handle<AmazonS3Exception>(x => x.StatusCode == HttpStatusCode.TooManyRequests || (int)x.StatusCode >= 500)
            .WaitAndRetryAsync(5, _ => TimeSpan.FromSeconds(1)),
        // Inner: timeout
        Policy.TimeoutAsync(TimeSpan.FromSeconds(15), TimeoutStrategy.Pessimistic)
    );

    private readonly object _lock = new();
    private SqliteConnection? _connection;

    public LibraryProvider(AccountSettingsProvider accountSettingsProvider, ProcessTempDir processTempDir)
    {
        _accountSettingsProvider = accountSettingsProvider;
        _processTempDir = processTempDir;
        _accountSettingsProvider.SettingsChanged += (s, e) => Connect();
    }

    public static void Create(string filePath)
    {
        SqliteConnectionStringBuilder builder = new() { DataSource = filePath, Pooling = false };
        using SqliteConnection connection = new(builder.ConnectionString);
        connection.Open();
    }

    public void Connect()
    {
        lock (_lock)
        {
            _connection?.Dispose();
            _connection = null;

            SqliteConnectionStringBuilder builder =
                new() { DataSource = _accountSettingsProvider.Current.DatabaseFilePath, Pooling = false };
            SqliteConnection connection = new(builder.ConnectionString);
            connection.Open();
            _connection = connection;

            Execute("PRAGMA foreign_keys = ON;");

            Upgrade();
        }
    }

    public void Disconnect()
    {
        lock (_lock)
        {
            _connection?.Dispose();
            _connection = null;
        }
    }

    public void Dispose()
    {
        Disconnect();
    }

    private static string GetResourceText(string filename)
    {
        return File.ReadAllText(
            Path.Combine(Path.GetDirectoryName(typeof(LibraryProvider).Assembly.Location)!, "Resources", filename)
        );
    }

    private int GetDatabaseVersion()
    {
        Execute(
            @"
CREATE TABLE IF NOT EXISTS file_version (
    version_number INTEGER PRIMARY KEY
) WITHOUT ROWID
"
        );

        using var command = _connection!.CreateCommand();
        command.CommandText = "SELECT version_number FROM file_version;";
        using var reader = command.ExecuteReader();
        return reader.Read() ? reader.GetInt32(0) : 0;
    }

    private void Upgrade()
    {
        using var transaction = _connection!.BeginTransaction();

        while (true)
        {
            var version = GetDatabaseVersion();
            if (version >= CURRENT_VERSION)
                break;

            var nextVersion = version + 1;

            Execute(
                GetResourceText($"Migrate-{nextVersion}.sql"),
                x =>
                {
                    x.AddWithValue("@new_tag_type_id1", new TagTypeId().Value);
                    x.AddWithValue("@new_tag_type_id2", new TagTypeId().Value);
                    x.AddWithValue("@new_tag_type_id3", new TagTypeId().Value);
                    x.AddWithValue("@new_tag_type_id4", new TagTypeId().Value);
                }
            );

            Execute(
                @"
DELETE FROM file_version;
INSERT INTO file_version VALUES (@n);
",
                p =>
                {
                    p.AddWithValue("@n", nextVersion);
                }
            );
        }

        transaction.Commit();
    }

    private List<T> Query<T>(
        string sql,
        Action<SqliteParameterCollection> configureParameters,
        Func<SqliteDataReader, T> generator
    )
    {
        lock (_lock)
        {
            using var command = _connection!.CreateCommand();
            command.CommandText = sql;
            configureParameters(command.Parameters);
            using var reader = command.ExecuteReader();
            List<T> rows = [];
            while (reader.Read())
                rows.Add(generator(reader));
            return rows;
        }
    }

    private T QuerySingle<T>(
        string sql,
        Action<SqliteParameterCollection> configureParameters,
        Func<SqliteDataReader, T> generator
    )
    {
        return Query(sql, configureParameters, generator).Single();
    }

    private void Execute(string sql, Action<SqliteParameterCollection>? configureParameters = null)
    {
        lock (_lock)
        {
            using var command = _connection!.CreateCommand();
            command.CommandText = sql;
            configureParameters?.Invoke(command.Parameters);
            command.ExecuteNonQuery();
        }
    }

    public void NewTagType(TagType tagType) =>
        Execute(
            """
            INSERT INTO tag_types (id, sort_index, singular_name, plural_name)
            VALUES (@id, @sort_index, @singular_name, @plural_name);
            """,
            p =>
            {
                p.AddWithValue("@id", tagType.Id.Value);
                p.AddWithValue("@sort_index", tagType.SortIndex);
                p.AddWithValue("@singular_name", tagType.SingularName);
                p.AddWithValue("@plural_name", tagType.PluralName);
            }
        );

    public void UpdateTagType(TagType tagType) =>
        Execute(
            """
            UPDATE tag_types
            SET sort_index = @sort_index, singular_name = @singular_name, plural_name = @plural_name
            WHERE id = @id;
            """,
            p =>
            {
                p.AddWithValue("@id", tagType.Id.Value);
                p.AddWithValue("@sort_index", tagType.SortIndex);
                p.AddWithValue("@singular_name", tagType.SingularName);
                p.AddWithValue("@plural_name", tagType.PluralName);
            }
        );

    public TagType GetTagType(TagTypeId id) =>
        QuerySingle(
            """
            SELECT id, sort_index, singular_name, plural_name
            FROM tag_types
            WHERE id = @id;
            """,
            p =>
            {
                p.AddWithValue("@id", id.Value);
            },
            r => new TagType(
                Id: new(r.GetString(0)),
                SortIndex: r.GetInt32(1),
                SingularName: r.GetString(2),
                PluralName: r.GetString(3)
            )
        );

    public void DeleteTagType(TagTypeId id) =>
        Execute(
            """
            DELETE FROM movie_tags WHERE tag_id IN (SELECT id FROM tags WHERE tag_type_id = @id);
            DELETE FROM tags WHERE tag_type_id = @id;
            DELETE FROM tag_types WHERE id = @id;
            """,
            p =>
            {
                p.AddWithValue("@id", id.Value);
            }
        );

    public void NewTag(Tag tag) =>
        Execute(
            "INSERT INTO tags (id, tag_type_id, name) VALUES (@id, @type, @name)",
            p =>
            {
                p.AddWithValue("@id", tag.Id.Value);
                p.AddWithValue("@type", tag.TagTypeId.Value);
                p.AddWithValue("@name", tag.Name);
            }
        );

    public void UpdateTag(Tag tag) =>
        Execute(
            "UPDATE tags SET name = @name WHERE id = @id",
            p =>
            {
                p.AddWithValue("@id", tag.Id.Value);
                p.AddWithValue("@name", tag.Name);
            }
        );

    public Tag GetTag(TagId id) =>
        QuerySingle(
            "SELECT id, tag_type_id, name FROM tags WHERE id = @id",
            p =>
            {
                p.AddWithValue("@id", id.Value);
            },
            r => new Tag(new TagId(r.GetString(0)), new(r.GetString(1)), r.GetString(2))
        );

    public void DeleteTag(TagId id) =>
        Execute(
            """
            DELETE FROM movie_tags WHERE tag_id = @id;
            DELETE FROM tags WHERE id = @id;
            """,
            p =>
            {
                p.AddWithValue("@id", id.Value);
            }
        );

    public List<Movie> GetMovies() =>
        Query(
            "SELECT id, filename, s3_key, date_added FROM movies",
            p => { },
            r => new Movie(
                Id: new(r.GetString(0)),
                Filename: r.GetString(1),
                S3Key: r.GetString(2),
                DateAdded: DateTimeOffset.Parse(r.GetString(3))
            )
        );

    public Movie GetMovie(MovieId id) =>
        QuerySingle(
            "SELECT id, filename, s3_key, date_added FROM movies WHERE id = @id",
            p =>
            {
                p.AddWithValue("@id", id.Value);
            },
            r => new Movie(
                Id: new(r.GetString(0)),
                Filename: r.GetString(1),
                S3Key: r.GetString(2),
                DateAdded: DateTimeOffset.Parse(r.GetString(3))
            )
        );

    public void UpdateMovie(Movie movie) =>
        Execute(
            "UPDATE movies SET filename = @filename WHERE id = @id",
            p =>
            {
                p.AddWithValue("@id", movie.Id.Value);
                p.AddWithValue("@filename", movie.Filename);
            }
        );

    public void DeleteMovie(MovieId id)
    {
        var movie = GetMovie(id);

        Execute(
            """
            DELETE FROM movie_files WHERE movie_id = @id;
            DELETE FROM movie_tags WHERE movie_id = @id;
            DELETE FROM movies WHERE id = @id;
            """,
            p =>
            {
                p.AddWithValue("@id", id.Value);
            }
        );

        using var s3 = _accountSettingsProvider.CreateAmazonS3Client();
        var bucket = _accountSettingsProvider.Current.Bucket;
        var s3Key = movie.S3Key;
        s3.DeleteObjectAsync(bucket, s3Key).GetAwaiter().GetResult();
    }

    public byte[] GetMovieClip(MovieId movieId) => GetMovieFileData(movieId, "clip.mp4");

    public byte[] GetM3u8(MovieId movieId, int portNumber, string sessionPassword)
    {
        var hostname = _accountSettingsProvider.Current.M3u8Hostname;
        if (string.IsNullOrWhiteSpace(hostname))
            hostname = "localhost";

        using MemoryStream input = new(GetMovieFileData(movieId, "movie.m3u8"));
        using StreamReader reader = new(input);
        using MemoryStream output = new();
        using (StreamWriter writer = new(output))
        {
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                var match = TsFilenameRegex().Match(line);
                if (match.Success)
                {
                    // Modify segment filename to point to our /movie.ts endpoint.
                    var tsIndex = int.Parse(match.Groups[1].Value);
                    var query = HttpUtility.ParseQueryString("");
                    query["sessionPassword"] = sessionPassword;
                    query["movieId"] = movieId.Value;
                    query["index"] = $"{tsIndex}";
                    writer.WriteLine($"http://{hostname}:{portNumber}/movie.ts?{query}");
                }
                else
                {
                    // All other lines are unchanged.
                    writer.WriteLine(line);
                }
            }
        }

        return output.ToArray();
    }

    public (long Offset, byte[] Data) GetZipHeader(MovieId movieId) =>
        QuerySingle(
            """
            SELECT f.offset, f.data
            FROM movie_files f
            WHERE
                f.movie_id = @id
                AND f.name = ''
            """,
            p =>
            {
                p.AddWithValue("@id", movieId.Value);
            },
            r =>
            {
                var offset = r.GetInt64(0);
                using var stream = (MemoryStream)r.GetStream(1);
                return (offset, stream.ToArray());
            }
        );

    public OffsetLength GetMovieFileOffsetLength(MovieId movieId, string name) =>
        QuerySingle(
            """
            SELECT f.offset, f.length
            FROM movie_files f
            WHERE
                f.movie_id = @id
                AND f.name = @name
            """,
            p =>
            {
                p.AddWithValue("@id", movieId.Value);
                p.AddWithValue("@name", name);
            },
            r =>
            {
                var offset = r.GetInt64(0);
                var length = r.GetInt64(1);
                return new OffsetLength(offset, (int)length);
            }
        );

    private byte[] GetMovieFileData(MovieId movieId, string name) =>
        QuerySingle(
            """
            SELECT f.data
            FROM movie_files f
            WHERE
                f.movie_id = @id
                AND f.name = @name
            """,
            p =>
            {
                p.AddWithValue("@id", movieId.Value);
                p.AddWithValue("@name", name);
            },
            r =>
            {
                using var stream = (MemoryStream)r.GetStream(0);
                return stream.ToArray();
            }
        );

    public List<TagType> GetTagTypes() =>
        Query(
            "SELECT id, sort_index, singular_name, plural_name FROM tag_types",
            p => { },
            r => new TagType(
                Id: new(r.GetString(0)),
                SortIndex: r.GetInt32(1),
                SingularName: r.GetString(2),
                PluralName: r.GetString(3)
            )
        );

    public List<Tag> GetTags() =>
        Query(
            "SELECT id, tag_type_id, name FROM tags",
            p => { },
            r => new Tag(Id: new(r.GetString(0)), TagTypeId: new(r.GetString(1)), Name: r.GetString(2))
        );

    public List<Tag> GetTags(TagTypeId tagTypeId) =>
        Query(
            "SELECT id, name FROM tags WHERE tag_type_id = @id",
            p =>
            {
                p.AddWithValue("@id", tagTypeId.Value);
            },
            r => new Tag(Id: new(r.GetString(0)), TagTypeId: tagTypeId, Name: r.GetString(1))
        );

    public List<MovieTag> GetMovieTags() =>
        Query(
            "SELECT movie_id, tag_id FROM movie_tags",
            p => { },
            r => new MovieTag(MovieId: new(r.GetString(0)), TagId: new(r.GetString(1)))
        );

    public List<MovieTag> GetMovieTags(MovieId movieId) =>
        Query(
            "SELECT movie_id, tag_id FROM movie_tags WHERE movie_id = @movie_id",
            p =>
            {
                p.AddWithValue("@movie_id", movieId.Value);
            },
            r => new MovieTag(MovieId: new(r.GetString(0)), TagId: new(r.GetString(1)))
        );

    public List<Movie> GetMoviesWithTag(HashSet<MovieId> filteredMovieIds, TagId tagId) =>
        Query(
                """
                SELECT m.id, m.filename, m.s3_key, m.date_added
                FROM movie_tags mt
                INNER JOIN movies m ON mt.movie_id = m.id
                WHERE mt.tag_id = @tag_id
                """,
                p =>
                {
                    p.AddWithValue("@tag_id", tagId.Value);
                },
                r => new Movie(
                    Id: new(r.GetString(0)),
                    Filename: r.GetString(1),
                    S3Key: r.GetString(2),
                    DateAdded: DateTimeOffset.Parse(r.GetString(3))
                )
            )
            .Where(x => filteredMovieIds.Contains(x.Id))
            .ToList();

    public void AddMovieTag(MovieId movieId, TagId tagId) =>
        Execute(
            "INSERT INTO movie_tags (movie_id, tag_id) VALUES (@movie_id, @tag_id)",
            p =>
            {
                p.AddWithValue("@movie_id", movieId.Value);
                p.AddWithValue("@tag_id", tagId.Value);
            }
        );

    public void DeleteMovieTag(MovieId movieId, TagId tagId) =>
        Execute(
            "DELETE FROM movie_tags WHERE movie_id = @movie_id AND tag_id = @tag_id",
            p =>
            {
                p.AddWithValue("@movie_id", movieId.Value);
                p.AddWithValue("@tag_id", tagId.Value);
            }
        );

    public void NewMovie(Movie movie) =>
        Execute(
            """
            INSERT INTO movies (id, filename, s3_key, date_added)
            VALUES (@id, @filename, @s3_key, @date_added)
            """,
            p =>
            {
                p.AddWithValue("@id", movie.Id.Value);
                p.AddWithValue("@filename", movie.Filename);
                p.AddWithValue("@s3_key", movie.S3Key);
                p.AddWithValue("@date_added", movie.DateAdded.ToString("O"));
            }
        );

    public void NewMovieFile(MovieFile file) =>
        Execute(
            """
            INSERT INTO movie_files (movie_id, name, offset, length, data)
            VALUES (@movie_id, @name, @offset, @length, @data)
            """,
            p =>
            {
                p.AddWithValue("@movie_id", file.MovieId.Value);
                p.AddWithValue("@name", file.Name);
                p.AddWithValue("@offset", file.OffsetLength.Offset);
                p.AddWithValue("@length", file.OffsetLength.Length);
                p.AddWithValue("@data", file.Data is null ? DBNull.Value : file.Data);
            }
        );

    public Dictionary<TagId, MovieId> GetRandomMoviePerTag(TagType type)
    {
        var rows = Query(
            """
            SELECT mt.tag_id, m.id AS movie_id
            FROM movie_tags mt
            INNER JOIN movies m ON mt.movie_id = m.id
            WHERE mt.tag_id IN (
                SELECT id
                FROM tags
                WHERE tag_type_id = @tag_type_id
            )
            """,
            p =>
            {
                p.AddWithValue("@tag_type_id", type.Id.Value);
            },
            r => (TagId: new TagId(r.GetString(0)), MovieId: new MovieId(r.GetString(1)))
        );

        return (from r in rows group r by r.TagId into g select g.ElementAt(new Random().Next(g.Count()))).ToDictionary(
            x => x.TagId,
            x => x.MovieId
        );
    }

    public void WithTransaction(Action action)
    {
        lock (_lock)
        {
            using var transaction = _connection!.BeginTransaction();
            action();
            transaction.Commit();
        }
    }

    public async Task SyncUpAsync(CancellationToken cancel)
    {
        var settings = _accountSettingsProvider.Current;
        using var s3 = _accountSettingsProvider.CreateAmazonS3Client();

        // Check object version to see if anything has changed.
        var changed = await HasRemoteVersionChangedAsync(s3, cancel).ConfigureAwait(false);
        if (changed)
        {
            await SyncDownAsync(cancel).ConfigureAwait(false);
            throw new Exception(
                "Another user updated the library at the same time and your change failed. Please try again."
            );
        }

        // Export library to JSON.
        SyncLibrary library =
            new(Movies: GetMovies(), TagTypes: GetTagTypes(), Tags: GetTags(), MovieTags: GetMovieTags());
        var json = JsonSerializer.Serialize(library);
        using MemoryStream jsonMemoryStream = new(Encoding.UTF8.GetBytes(json));

        // Create password protected Zip.
        using MemoryStream zipMemoryStream = new();
        EncryptedZipFile.CreateLibraryZip(zipMemoryStream, "library.json", jsonMemoryStream, settings.Password);
        zipMemoryStream.Position = 0;

        // Upload to S3.
        var putObjectResponse = await _policy
            .ExecuteAsync(
                async cancel =>
                    await s3.PutObjectAsync(
                            new()
                            {
                                BucketName = settings.Bucket,
                                Key = "library.zip",
                                InputStream = zipMemoryStream,
                            },
                            cancel
                        )
                        .ConfigureAwait(false),
                cancel
            )
            .ConfigureAwait(false);

        // Store the object version so we know later whether the library has changed.
        var etag = putObjectResponse.ETag;
        UpdateRemoteVersion(etag);
    }

    private void UpdateRemoteVersion(string etag)
    {
        WithTransaction(() =>
        {
            Execute(
                """
                DELETE FROM remote_version;
                INSERT INTO remote_version (version_id) VALUES (@version_id);
                """,
                x =>
                {
                    x.AddWithValue("@version_id", etag);
                }
            );
        });
    }

    public async Task<bool> HasRemoteVersionChangedAsync(IAmazonS3 s3, CancellationToken cancel)
    {
        var settings = _accountSettingsProvider.Current;

        // Check object version to see if anything has changed.
        var localVersionId = Query(
                "SELECT version_id FROM remote_version",
                _ => { },
                r => r.IsDBNull(0) ? null : (string?)r.GetString(0)
            )
            .SingleOrDefault();

        string? remoteVersionId;
        try
        {
            var metadataResponse = await _policy
                .ExecuteAsync(
                    async cancel =>
                        await s3.GetObjectMetadataAsync(settings.Bucket, "library.zip", cancel).ConfigureAwait(false),
                    cancel
                )
                .ConfigureAwait(false);
            remoteVersionId = metadataResponse.ETag;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // The file isn't there.
            remoteVersionId = null;
        }

        return localVersionId != remoteVersionId;
    }

    public async Task SyncDownAsync(CancellationToken cancel)
    {
        var settings = _accountSettingsProvider.Current;
        using var s3 = _accountSettingsProvider.CreateAmazonS3Client();

        // Check object version to see if anything has changed.
        var changed = await HasRemoteVersionChangedAsync(s3, cancel).ConfigureAwait(false);
        if (!changed)
            return;

        // Download from S3.
        Amazon.S3.Model.GetObjectResponse response;
        try
        {
            response = await _policy
                .ExecuteAsync(
                    async cancel =>
                        await s3.GetObjectAsync(settings.Bucket, "library.zip", cancel).ConfigureAwait(false),
                    cancel
                )
                .ConfigureAwait(false);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // The file isn't there. Nothing to sync.
            return;
        }
        using MemoryStream responseMemoryStream = new();
        using var responseStream = response.ResponseStream;
        await responseStream.CopyToAsync(responseMemoryStream, cancel).ConfigureAwait(false);
        responseMemoryStream.Position = 0;

        // Extract password protected Zip.
        using MemoryStream jsonStream = new();
        EncryptedZipFile.ExtractSingleFile(responseMemoryStream, "library.json", jsonStream, settings.Password);
        jsonStream.Position = 0;

        // Parse the JSON.
        var remote = JsonSerializer.Deserialize<SyncLibrary>(jsonStream);

        // Prepare to compare local vs. remote.
        var remoteMovieTags = remote.MovieTags.ToDictionary(x => (x.MovieId, x.TagId));
        var remoteTagTypes = remote.TagTypes.ToDictionary(x => x.Id);
        var remoteTags = remote.Tags.ToDictionary(x => x.Id);
        var remoteMovies = remote.Movies.ToDictionary(x => x.Id);

        var localMovieTags = GetMovieTags().ToDictionary(x => (x.MovieId, x.TagId));
        var localTagTypes = GetTagTypes().ToDictionary(x => x.Id);
        var localTags = GetTags().ToDictionary(x => x.Id);
        var localMovies = GetMovies().ToDictionary(x => x.Id);

        // We have to issue S3 requests for parts of the movie files in order to populate the movie_files table, since
        // that information is not in the JSON. Do this before opening a transaction because it may take awhile.
        var newMovieIds = remoteMovies.Keys.Except(localMovies.Keys);
        using var tempDir = _processTempDir.NewDir();
        SyncMovieFileReader syncMovieFileReader = new(settings.Bucket, settings.Password, tempDir.Path, s3);
        var remoteMoviesByS3Key = remoteMovies.Values.ToDictionary(x => x.S3Key);
        var cachedFiles = await syncMovieFileReader
            .ReadZipEntriesToBeCachedLocallyAsync(newMovieIds.Select(id => remoteMovies[id].S3Key).ToList(), cancel)
            .ConfigureAwait(false);

        WithTransaction(() =>
        {
            // Update columns of rows that are in both the local and remote libraries.
            foreach (var tagTypeId in localTagTypes.Keys.Intersect(remoteTagTypes.Keys))
                UpdateTagType(remoteTagTypes[tagTypeId]);

            foreach (var tagId in localTags.Keys.Intersect(remoteTags.Keys))
                UpdateTag(remoteTags[tagId]);

            foreach (var movieId in localMovies.Keys.Intersect(remoteMovies.Keys))
                UpdateMovie(remoteMovies[movieId]);

            // Add local rows that are new in the remote library.
            foreach (var tagTypeId in remoteTagTypes.Keys.Except(localTagTypes.Keys))
                NewTagType(remoteTagTypes[tagTypeId]);

            foreach (var tagId in remoteTags.Keys.Except(localTags.Keys))
                NewTag(remoteTags[tagId]);

            foreach (var movieId in newMovieIds)
                NewMovie(remoteMovies[movieId]);

            foreach (var x in remoteMovieTags.Keys.Except(localMovieTags.Keys))
                AddMovieTag(x.MovieId, x.TagId);

            // Delete local rows no longer present in the remote library.
            foreach (var tagTypeId in localTagTypes.Keys.Except(remoteTagTypes.Keys))
                DeleteTagType(tagTypeId);

            foreach (var movieTagId in localMovieTags.Keys.Except(remoteMovieTags.Keys))
                DeleteMovieTag(movieTagId.MovieId, movieTagId.TagId);

            foreach (var tagId in localTags.Keys.Except(remoteTags.Keys))
                DeleteTag(tagId);

            foreach (var movieId in localMovies.Keys.Except(remoteMovies.Keys))
                DeleteMovie(movieId);

            // Populate the movie_files table.
            foreach (var x in cachedFiles)
            {
                var movie = remoteMoviesByS3Key[x.Key];
                var bytes = x.FilePath is null ? null : File.ReadAllBytes(x.FilePath);
                MovieFile movieFile = new(movie.Id, x.EntryName, x.OffsetLength, bytes);
                NewMovieFile(movieFile);
            }
        });

        UpdateRemoteVersion(response.ETag);
    }

    private readonly record struct SyncLibrary(
        List<Movie> Movies,
        List<TagType> TagTypes,
        List<Tag> Tags,
        List<MovieTag> MovieTags
    );

    [GeneratedRegex(@"movie(\d+).ts")]
    private static partial Regex TsFilenameRegex();
}
