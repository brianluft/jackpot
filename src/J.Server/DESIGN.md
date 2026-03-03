# J.Server Design

ASP.NET Core HTTP server that serves media content and UI pages to the embedded WebView2 browser and optional LAN clients. Runs as a subprocess of J.App.

## Target

- `net9.0-windows`, SDK `Microsoft.NET.Sdk.Web`, output `WinExe`, assembly name `Jackpot.Server`
- GC tuned for low memory: server GC off, concurrent GC on, LOH compaction enabled

## Dependencies

- **J.Core** - All data access, encryption, S3 integration
- **Microsoft.WindowsDesktop.App** (framework ref) - For DPAPI support

## Static Assets (bundled in `static/`)

- `hls.min.js` - HLS.js for adaptive bitrate streaming
- `tabulator.min.js` / `tabulator.min.css` - Tabulator.js for list-view tables
- `video.min.js` / `video-js.min.css` - Video.js player
- `favicon.ico`

All static files are loaded into memory at startup.

## Authentication

Sensitive endpoints require a `sessionPassword` query parameter - a random per-session GUID set by J.App via the `JACKPOT_SESSION_PASSWORD` environment variable. Network sharing (browser) endpoints do not require it.

## Endpoints

### Media Streaming

| Endpoint | Auth | Description |
|----------|------|-------------|
| `GET /movie.m3u8` | Yes | HLS playlist with rewritten `.ts` segment URLs |
| `GET /movie.ts` | Yes | Single decrypted MPEG-TS segment (fetched from S3 via byte-range, decrypted on the fly) |
| `GET /clip.mp4` | Yes | Preview clip (served from local SQLite cache) |

The `/movie.ts` endpoint is the most complex: it fetches only the specific encrypted segment bytes from S3 using an HTTP range request, combines them with the locally-cached ZIP central directory via `SparseDataStream`, and decrypts using `EncryptedZipFile.ReadEntry()`. This avoids downloading the full encrypted ZIP.

### HTML Pages (WebView2 UI)

| Endpoint | Description |
|----------|-------------|
| `GET /list.html` | Main library listing - grid wall or Tabulator table based on preferences |
| `GET /tag.html` | Movies filtered to a specific tag |
| `GET /untagged.html` | Movies missing a tag for a given tag type |
| `GET /movie-preview.html` | Looping muted clip preview |
| `GET /movie-play.html` | Full HLS video player using hls.js |

Pages communicate back to the WinForms host via `window.chrome.webview.postMessage()` for navigation, context menus, and search.

### Actions

| Endpoint | Description |
|----------|-------------|
| `POST /reshuffle` | Regenerate shuffle seed |
| `POST /inhibit-scroll-restore` | Prevent scroll restoration on next page load |

### Network Sharing (conditional, no auth)

Registered only when `Preferences.NetworkSharing_AllowWebBrowserAccess` is true. Simple HTML pages for LAN browsers:

| Endpoint | Description |
|----------|-------------|
| `GET /` | Index: links to Movies and tag type folders |
| `GET /browser-movies.html` | All movies as links |
| `GET /browser-tagtype.html` | Tags within a tag type |
| `GET /browser-tag.html` | Movies with a specific tag |

## HTML Generation

All HTML is generated server-side via C# string interpolation. No Razor views or template engines.

### Page Types

- **WallPage (Grid)** - Virtualized video grid with configurable column count. Absolute-positioned cells, only renders DOM elements within a scroll buffer. Each cell plays `clip.mp4` in a looping muted `<video>`.
- **ListPage (Table)** - Tabulator.js virtualized table with columns for Name, tag types, and Date Added.
- **EmptyPage** - Shown when no movies match the filter.
- **PageShared** - Common JavaScript included in all WebView2 pages: context menu handling, keyboard shortcuts (Ctrl+F, /), navigation functions, cookie-based scroll position persistence keyed by a `filterSortHash` (SHA-256 of filter+sort settings).

## Middleware

- **`RequestLoggingMiddleware`** - Assigns monotonic `RequestId`, logs start/completion with timing
- **`CustomConsoleFormatter`** - Message-only log format (no ASP.NET prefix)
- **`RequestIdProvider`** / **`RequestLogger`** - Per-request scoped logging with sub-step timing

## Retry Policy

`ServerPolicy.Policy` - Polly async retry up to 5 times on any exception. Wraps `/movie.ts` S3 fetches.

## Key Integration Point: `ServerMovieFileReader`

Transient service that handles the sparse-fetch optimization for `/movie.ts`:
1. Look up S3 key and segment offset/length from SQLite
2. Fetch encrypted bytes from S3 via byte-range request
3. Fetch ZIP header from SQLite
4. Assemble `SparseDataStream` with ZIP magic + header + segment bytes
5. Decrypt via `EncryptedZipFile.ReadEntry()`
6. Stream decrypted bytes to response
