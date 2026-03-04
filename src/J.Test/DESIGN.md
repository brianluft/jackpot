# J.Test Design

Unit and integration tests for J.Core components.

## Target

- `net10.0-windows`
- **MSTest** 3.6.4 (`MSTest.TestFramework`, `MSTest.TestAdapter`)
- `Microsoft.NET.Test.Sdk` 17.12.0
- `coverlet.collector` 6.0.2 for code coverage

## Project Reference

- **J.App** (which transitively includes J.Core and J.Server)

## Test Infrastructure

**`TestDir`** - Shared static helper providing a fixed temp directory at `%TEMP%\J.Test` with `Clear()` for per-test cleanup.

## Test Classes

### `SparseDataStreamTest` (11 tests)

Tests `J.Core.SparseDataStream` - the read-only seekable stream that serves data from pre-populated byte-range islands.

Covers: initialization, reading within/outside sparse ranges, multi-range reads, seeking, stream capability properties, boundary violations.

### `TrackingFileStreamTest` (5 tests)

Tests `J.Core.TrackingFileStream` - the `FileStream` decorator that records the bounding byte range of all reads.

Uses a 100-byte test file. Covers: initial null state, range tracking after single/multiple reads, `ClearReadRange()` reset behavior, post-clear tracking.

### `EncryptedZipFileTest` (2 tests)

Integration tests for `J.Core.EncryptedZipFile`:

- **`TextCreateAndExtract`** - Round-trip: create encrypted ZIP with two small files, extract, verify contents match.
- **`TestSparseUsage`** - Full sparse-access workflow: create encrypted ZIP with 10 x 1MB files, build `ZipIndex`, manually construct `SparseDataStream` from only the needed byte ranges, extract a single entry via `ReadEntry`, verify byte-for-byte match with original.

## Running

```
dotnet test src/J.Test
```

Note: Tests are not currently included in the CI workflow.
