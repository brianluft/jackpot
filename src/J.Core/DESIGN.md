# J.Core Design

Shared library providing data access, cloud storage, encryption, and system utilities. Referenced by all other projects.

## Target

- `net10.0-windows`, nullable enabled, unsafe blocks allowed

## Dependencies

- **AWSSDK.S3** / **AWSSDK.SecurityToken** - S3-compatible cloud storage
- **Microsoft.Data.Sqlite** - Local SQLite databases
- **Polly** - Retry/timeout resilience policies
- **SharpZipLib** - AES-256 encrypted ZIP creation and extraction
- **System.Security.Cryptography.ProtectedData** - Windows DPAPI for credential storage

## Namespaces

- `J.Core` - Services and utilities
- `J.Core.Data` - Pure data types (records, enums, value objects)

## Domain Model (`J.Core.Data`)

### Strongly-Typed IDs

`IdBase<T>` generates random 16-char IDs with a mandatory prefix (e.g. `movie-a1b2c3d4e5f6g7h8`). Concrete subtypes:

| Type | Prefix |
|------|--------|
| `MovieId` | `movie-` |
| `TagId` | `tag-` |
| `TagTypeId` | `tagtype-` |

### Entities

- **`Movie`** - `(Id, Filename, S3Key, DateAdded, Deleted)`. Soft-delete via `Deleted` flag.
- **`TagType`** - `(Id, SortIndex, SingularName, PluralName)`. Categories of tags (e.g. Actor, Series, Studio, Category).
- **`Tag`** - `(Id, TagTypeId, Name)`. A concrete tag value within a tag type.
- **`MovieTag`** - `(MovieId, TagId)`. Many-to-many join.
- **`MovieFile`** - `(MovieId, Name, OffsetLength, Data)`. One entry within a movie's encrypted ZIP. `Name` is the ZIP entry name (`""` = header, `movie.m3u8`, `clip.mp4`, `movieN.ts`). `Data` is only stored locally for the header, m3u8, and clip.

### Value Objects

- **`AccountSettings`** - S3 endpoint, credentials, bucket, and encryption password.
- **`Password`** - Validated non-empty password. `Password.New()` generates a cryptographically random 32-char password.
- **`OffsetLength`** - `(Offset, Length)` byte-range descriptor.
- **`ZipEntryLocation`** / **`ZipIndex`** - Maps ZIP entry names to byte ranges within the S3 object. Serialized as JSON and appended to the end of every movie ZIP file for byte-range streaming.
- **`Filter`** / **`FilterRule`** / **`FilterField`** - Composable filter model (AND/OR rules by tag or filename).
- **`SortOrder`** - `(Shuffle, Ascending, Field)`.

### Enums

- `FilterFieldType` (Filename, TagType), `FilterOperator` (IsTag, IsNotTag, IsTagged, etc.)
- `LibraryViewStyle` (Grid, List), `MoviePlayerToUse` (Automatic, Vlc, Integrated, browsers)
- `WindowMaximizeBehavior` (Fullscreen, Windowed)

## Services

### `AccountSettingsProvider`

Thread-safe singleton for S3 credentials. Encrypts settings on disk with Windows DPAPI (`ProtectedData.Protect`, scope `CurrentUser`) at `%APPDATA%\Jackpot\account-settings`. Creates `AmazonS3Client` instances from stored settings.

### `LibraryProvider`

Central data-access layer. Manages the local SQLite database at `%LOCALAPPDATA%\Jackpot\library.db` and syncs with S3.

**Sync architecture:**
- **SyncUp** - Serializes the library as JSON, wraps in AES-256 encrypted `library.zip`, uploads to S3. Uses ETags for optimistic concurrency.
- **SyncDown** - Downloads `library.zip`, decrypts, performs 3-way diff (local vs. remote), and for new movies fetches ZIP indices and caches headers/m3u8/clips from S3 using parallel fetches (up to 64 concurrency).

**HLS playlist rewriting (`GetM3u8`)** - Reads the raw `movie.m3u8` from the DB and rewrites `.ts` segment URLs to point at the local server.

### `SyncMovieFileReader`

Three-phase pipeline for downloading movie metadata during sync:
1. Download ZIP indices (last 100KB of each S3 object) - up to 64 parallel
2. Download raw encrypted entry data (header, m3u8, clip) - up to 64 parallel
3. Decrypt entries using `SparseDataStream` - up to `ProcessorCount-1` parallel

### `EncryptedZipFile`

Static utility for all ZIP operations using SharpZipLib with AES-256 encryption.

- `CreateMovieZip` - Creates encrypted ZIP from a directory, appends a `ZipIndex` JSON trailer with an 8-byte offset footer.
- `GetZipIndex` - Reverse-engineers byte ranges of each ZIP entry using `TrackingFileStream`.
- `ReadEntry` / `ExtractSingleFile` / `ExtractToDirectory` - Decryption and extraction.

### `SparseDataStream`

Read-only seekable `Stream` that presents sparse data: only specific byte ranges are filled in. Used to assemble a virtual ZIP from separately-fetched byte ranges, enabling decryption of individual entries without downloading the full file.

### `TrackingFileStream`

`FileStream` decorator that records the bounding byte range of all reads. Used by `GetZipIndex` to discover where SharpZipLib reads each entry.

### `Preferences`

Thread-safe singleton persisting user preferences in SQLite at `%APPDATA%\Jackpot\preferences.db`. Typed accessors for bool, int, double, string, blob, enum, and JSON values.

### System Utilities

- **`Ffmpeg`** - Static wrapper for `ffmpeg.exe`/`ffprobe.exe` subprocesses. Codec detection, duration parsing, power throttling disabled.
- **`ApplicationSubProcesses`** - Windows Job Object ensuring child processes die with the parent.
- **`PowerThrottlingUtil`** - Disables CPU power throttling for a process via Win32 API.
- **`ProcessTempDir`** / **`TempDir`** - Process-scoped temp directories with exclusive file locks and cleanup of stale dirs.
- **`LanIpFinder`** - Finds the machine's LAN IP (RFC1918 private addresses).
- **`TaskbarUtil`** - Sets the Windows taskbar App User Model ID.
- **`MyColors`** - ~60 named `Color` constants for the dark-theme palette.
- **`GlobalLocks`** - A single `Lock` (`BigCpu`) to serialize CPU-intensive operations.

### DI Registration

`services.AddCore()` registers `AccountSettingsProvider`, `LibraryProvider`, `ProcessTempDir`, and `Preferences` as singletons.

## Database Schema (library.db)

All tables use `WITHOUT ROWID`. Schema version: 2.

```
file_version    (version_number INTEGER PK)
movies          (id TEXT PK, filename, s3_key, date_added, deleted)
tag_types       (id TEXT PK, sort_index, singular_name, plural_name)
tags            (id TEXT PK, tag_type_id FK, name)
movie_tags      (movie_id FK, tag_id FK, PK(movie_id, tag_id))
movie_files     (movie_id FK, name, offset, length, data BLOB NULL, PK(movie_id, name))
remote_version  (version_id TEXT PK)  -- S3 ETag for optimistic concurrency
```
