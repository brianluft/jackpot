# J.App Design

Windows Forms desktop application - the main entry point for Jackpot. Provides the UI for browsing, importing, tagging, and playing movies.

## Target

- `net10.0-windows`, WinForms, output `WinExe`, assembly name `Jackpot`
- `PublishReadyToRun = true`, unsafe blocks allowed

## Dependencies

- **J.Core** - Data access, encryption, S3, preferences
- **J.Server** - Spawned as a subprocess for media serving
- **Humanizer.Core** - Human-readable time durations in import stats
- **Microsoft.Web.WebView2** - Embedded Chromium browser for library UI and playback

## Architecture Overview

The app uses a hybrid architecture: native WinForms for chrome, dialogs, import, and tag management, with an embedded WebView2 (Chromium) for the library browser and video playback. WebView2 loads pages served by the J.Server subprocess and communicates back to the host via `window.chrome.webview.postMessage()`.

All UI is constructed in code (no XAML, no designer files). The `Ui` factory class provides DPI-aware control creation with a consistent dark theme.

## Startup Flow (`Program.cs`)

1. Configure WebView2, set dark mode, build DI container
2. Enforce single instance via `SingleInstanceManager` (Win32 Mutex)
3. If credentials exist: show `ProgressForm` that connects to S3, syncs library, starts `Client`, syncs M3U8 files, then shows `MainForm`
4. Otherwise: show `LoginForm` first

## Key Services

### `Client`

Manages the `Jackpot.Server.exe` subprocess on port 777. Passes a random `JACKPOT_SESSION_PASSWORD` via environment variable. Provides `GetMoviePlayerUrl(movieId)` and HTTP API calls (`ReshuffleAsync`, `InhibitScrollRestoreAsync`). Buffers up to 1000 lines of server output for diagnostics.

### `LibraryProviderAdapter`

Thread-safe facade over `LibraryProvider`. Every mutation follows: SyncDown (40%) -> local mutation -> SyncUp (40%) -> M3u8FolderSync (20%). Uses `SemaphoreSlim(1,1)` to serialize mutations.

### `S3Uploader`

Multi-threaded S3 multipart uploader: 8 worker threads, 10 MB part size, up to 10 retries (Polly). Tracks upload bytes for stats display.

### `Importer`

Per-file import orchestrator:
1. Get duration via FFmpeg
2. Generate 10-second preview clip (10 x 1s clips from throughout the movie)
3. HLS segmentation + AES-256 ZIP encryption via `MovieEncoder`
4. Multipart upload via `S3Uploader`
5. Register in library

### `MovieEncoder`

FFmpeg HLS segmentation -> encrypted ZIP packaging. Segment duration scales with movie length (5s per 45 min).

### `MovieConverter`

Converts incompatible codecs to H.264/AAC MP4 via FFmpeg with configurable CRF, preset, and audio bitrate.

### `MovieExporter`

Downloads encrypted ZIP from S3, decrypts, reassembles HLS segments to MP4 via `ffmpeg -codec copy`.

### `ImportQueue`

Batch import work queue with 2 parallel workers. Backed by a `DataTable`. Supports 30+ video formats. Deduplicates against existing library.

### `M3u8FolderSync`

Maintains a local folder of `.m3u8` playlist files for VLC network sharing, organized as `Movies/` and `{TagType}/{Tag}/`. Incremental updates only.

## Forms

### `MainForm`

Borderless window (1600x900 default) with custom `ToolStrip` title bar. Layout:
- **ToolStrip** - Tab buttons (Browser/Tags/Import), navigation (back/forward/refresh/home), menu, sort, view, filter, search
- **MyWebView2** - Library browser filling the client area
- **ImportControl** / **TagsControl** - Overlay panels for the Import and Tags tabs

Context menu on movies (via WebView2 messages): Play, Add tag, Export, Delete, Properties.

### Dialog Forms

| Form | Purpose |
|------|---------|
| `LoginForm` | S3 credential entry (endpoint, bucket, keys, password) |
| `MoviePlayerForm` | Borderless WebView2 window for HLS playback |
| `ProgressForm` | Modal/modeless async progress with cancel |
| `OptionsForm` | Settings: grid columns, player choice, fullscreen style, network sharing |
| `MoviePropertiesForm` | Edit movie name and tags with WebView2 preview |
| `AddTagsToMoviesForm` | Batch-add tags to multiple movies |
| `RecycleBinForm` | Restore or permanently delete soft-deleted movies |
| `AboutForm` | Version, links, diagnostic log |
| `MessageForm` | Custom MessageBox with 500ms button delay |
| `RenameMovieForm` | Rename a single movie |
| `FilterChooseTagForm` / `FilterEnterStringForm` | Filter builder dialogs |
| `TagForm` / `TagTypeForm` | Create/rename tags and tag groups |
| `VlcLaunchProgressForm` | Wait splash while VLC launches |

### UserControls

- **`ImportControl`** - Drag-and-drop import panel with DataGridView, animated progress cells, conversion settings, and stats
- **`TagsControl`** - Two-pane tag management: tag groups (left) and tags (right)

## Custom Controls

All custom-painted for the dark theme:

- **`Ui`** - Central factory. DPI scaling (`DeviceDpi/96`), font management, `MyToolStripRenderer` (full custom dark ToolStrip renderer with tab-appearance buttons)
- **`MyButton`** - Anti-aliased rounded rectangle button with hover/press states
- **`MyTextBox`** - Rounded border with focus accent line and placeholder text
- **`MyLabel`** - Custom enabled/disabled color handling
- **`MyTabControl`** / **`MyTabPage`** - Dark-themed tabs with rounded top corners
- **`MyWebView2`** - Pre-initialized WebView2 with locked-down settings and no caching
- **`MyToolStripTextBox`** - ToolStrip text box with placeholder text rendering

## UI Patterns

- **No XAML or designer** - All layout via `FlowLayoutPanel` and `TableLayoutPanel` in constructor code
- **DPI-aware** - Every pixel value passes through `ui.GetLength()` / `ui.GetSize()`
- **Dark theme** - `MyColors` defines all colors; custom renderers handle painting
- **Async work** - `ProgressForm.Do()` for modal async with cancellation
- **WebView2 bridge** - `WebMessageReceived` JSON messages trigger native context menu operations
- **DI** - `Microsoft.Extensions.DependencyInjection`; forms are transient, services are singletons
