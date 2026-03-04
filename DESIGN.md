# Jackpot Media Library - Design Overview

A personal video library application that stores movies in S3-compatible cloud storage (Backblaze B2) with end-to-end AES-256 encryption. Windows desktop app with network sharing capabilities.

## How It Works

1. Users import video files, which are converted to H.264 HLS segments, packaged into AES-256 encrypted ZIPs, and uploaded to S3.
2. Library metadata (movies, tags, tag types) is synced as an encrypted `library.zip` in S3 with optimistic concurrency via ETags.
3. A custom ZIP index appended to each movie file enables byte-range streaming - individual HLS segments are fetched and decrypted on demand without downloading the full file.
4. The app runs a local HTTP server (J.Server) that decrypts and streams content to an embedded Chromium browser (WebView2) and optionally to LAN devices.

## Architecture

```
+------------------+       +------------------+       +------------------+
|     J.App        |       |    J.Server      |       |   S3 (B2)        |
|  (WinForms UI)   |------>| (ASP.NET HTTP)   |------>| (Encrypted ZIPs) |
|  + WebView2      |<------| Port 777         |<------| + library.zip    |
+------------------+       +------------------+       +------------------+
        |                          |
        v                          v
+------------------+       +------------------+
|     J.Core       |       |   SQLite DBs     |
| (Shared Library) |       | library.db       |
|                  |       | preferences.db   |
+------------------+       +------------------+
```

- **J.App** - WinForms desktop application. Hosts the UI, manages import/export, spawns J.Server as a subprocess.
- **J.Server** - ASP.NET Core HTTP server. Serves HTML pages to WebView2, streams decrypted video, handles LAN sharing.
- **J.Core** - Shared library. Data access (SQLite), S3 operations, encryption/decryption, domain model, preferences, system utilities.
- **J.Test** - MSTest unit and integration tests for J.Core components.

## Key Design Decisions

- **Encrypted ZIP with appended index** - Each movie is a single S3 object containing AES-256 encrypted HLS segments. A JSON `ZipIndex` appended to the file maps entry names to byte ranges, enabling HTTP Range requests for individual segments.
- **SparseDataStream** - Decrypts individual ZIP entries by assembling a virtual stream from only the needed byte ranges (ZIP header + target entry), avoiding full-file downloads.
- **Hybrid WinForms + WebView2** - Native controls for app chrome, dialogs, and data management. Embedded Chromium for the rich library browser (virtualized video grid, animated clips) and HLS playback.
- **No XAML, no designer files** - All UI is constructed in code via the `Ui` factory class with DPI-aware scaling.
- **DPAPI credential storage** - S3 credentials encrypted at rest using Windows Data Protection API (per-user scope).
- **Optimistic concurrency** - Library sync uses S3 ETags to detect concurrent edits across multiple machines.

## Project Designs

- [J.Core](src/J.Core/DESIGN.md) - Shared library: domain model, data access, encryption, S3, utilities
- [J.App](src/J.App/DESIGN.md) - WinForms desktop application: UI, import/export, subprocess management
- [J.Server](src/J.Server/DESIGN.md) - ASP.NET Core HTTP server: media streaming, HTML generation, LAN sharing
- [J.Test](src/J.Test/DESIGN.md) - MSTest unit and integration tests

## Build and Distribution

- **Solution:** `src/jackpot.sln` (Visual Studio 2022, .NET 10)
- **License:** 0BSD
- **CI:** GitHub Actions on WarpBuild Windows runner (`warp-windows-latest-x64-8x`)
- **Architectures:** x64 and arm64, built in parallel
- **Distribution:** Sideload MSIX bundle via GitHub Releases
- **FFmpeg:** Downloaded at build time from GitHub releases (GPL builds)
- **Formatting:** CSharpier v0.29.2 (`src/.config/dotnet-tools.json`, run via `src/Format-Code.ps1`)
- **Version:** Hardcoded `1.0.0.0` in `src/AppxManifest.xml`
