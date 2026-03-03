# Jackpot Media Library

A Windows desktop app for managing a personal video library stored in S3-compatible cloud storage (Backblaze B2) with end-to-end AES-256 encryption.

## Quick Orientation

- **Solution:** `src/jackpot.sln` (.NET 9, C#, Windows-only)
- **J.Core** (`src/J.Core/`) - Shared library: domain model, SQLite data access, S3 operations, AES-256 encrypted ZIP handling, preferences, system utilities
- **J.App** (`src/J.App/`) - WinForms desktop app with embedded WebView2 browser. Entry point: `Program.cs`. Assembly: `Jackpot.exe`
- **J.Server** (`src/J.Server/`) - ASP.NET Core HTTP server spawned as a subprocess by J.App. Serves HTML to WebView2, streams decrypted video. Assembly: `Jackpot.Server.exe`
- **J.Test** (`src/J.Test/`) - MSTest tests for J.Core components

See [DESIGN.md](../DESIGN.md) and each project's `DESIGN.md` for detailed architecture.

## Build

```powershell
dotnet build src/jackpot.sln
dotnet test src/J.Test
dotnet run --project src/J.App     # runs the app
```

Full MSIX packaging: `src/Publish-MsixBundle.ps1` (requires Windows SDK).

## Code Style

- **Formatter:** CSharpier v0.29.2 - run `dotnet tool run dotnet-csharpier .` from `src/`
- **CA2007** is enforced as an error in J.App and J.Core - always use `ConfigureAwait(false)` on awaited tasks
- **No XAML or designer files** - all UI is constructed in code using the `Ui` factory class
- **DPI-aware** - all pixel values go through `ui.GetLength()` / `ui.GetSize()` (scales by `DeviceDpi / 96`)
- **Dark theme** - colors defined in `J.Core/MyColors.cs`, custom-painted controls (`MyButton`, `MyTextBox`, `MyTabControl`, etc.)

## Key Patterns

- **Strongly-typed IDs** - `MovieId`, `TagId`, `TagTypeId` extend `IdBase<T>` with prefix validation
- **Encrypted ZIP with appended index** - Movies are AES-256 encrypted ZIPs with a JSON `ZipIndex` trailer enabling byte-range S3 fetches
- **SparseDataStream** - Virtual stream from partial byte ranges for decrypting individual ZIP entries without downloading the full file
- **Sync model** - Library metadata synced as encrypted `library.zip` in S3 with ETag-based optimistic concurrency
- **LibraryProviderAdapter** - All mutations follow: SyncDown -> mutate -> SyncUp -> M3u8FolderSync (serialized via semaphore)
- **WebView2 bridge** - HTML pages use `window.chrome.webview.postMessage()` to communicate with the WinForms host

## Data Storage

- **S3 bucket** - Encrypted movie ZIPs + `library.zip` (metadata)
- `%LOCALAPPDATA%\Jackpot\library.db` - Local SQLite cache of library metadata and cached movie file entries
- `%APPDATA%\Jackpot\preferences.db` - User preferences (SQLite)
- `%APPDATA%\Jackpot\account-settings` - S3 credentials encrypted with Windows DPAPI

## CI/CD

GitHub Actions on WarpBuild Windows runner. Builds x64 + arm64 MSIX bundles (Store and Sideload variants). Tests are not run in CI.
