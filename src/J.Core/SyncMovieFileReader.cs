using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using J.Core.Data;
using Polly;
using Polly.Retry;

namespace J.Core;

public sealed class SyncMovieFileReader(string bucket, Password password, string dir, IAmazonS3 s3)
{
    private static readonly AsyncRetryPolicy _policy = Policy.Handle<Exception>().RetryAsync(5);

    private string NewFilePath() => Path.Combine(dir, Path.GetRandomFileName());

    public readonly record struct EntryData(string Key, string EntryName, OffsetLength OffsetLength, string? FilePath);

    public async Task<List<EntryData>> ReadZipEntriesToBeCachedLocallyAsync(
        List<string> keys,
        Action<double> updateProgress,
        CancellationToken cancel
    )
    {
        const double DOWNLOAD_INDICES_PORTION = 0.2;
        const double DOWNLOAD_DATA_PORTION = 0.6;
        const double EXTRACT_PORTION = 0.2;
        Debug.Assert(DOWNLOAD_INDICES_PORTION + DOWNLOAD_DATA_PORTION + EXTRACT_PORTION == 1);

        var count = keys.Count;
        var soFar = 0; // interlocked

        // Read the zip indices.
        object zipIndicesLock = new();
        Dictionary<string, ZipIndex> zipIndices = []; // Key => ZipIndex
        await Parallel
            .ForEachAsync(
                keys,
                new ParallelOptions { MaxDegreeOfParallelism = 64, CancellationToken = cancel },
                async (key, cancel) =>
                {
                    var zipIndex = await ReadZipIndexAsync(key, cancel).ConfigureAwait(false);

                    lock (zipIndicesLock)
                    {
                        zipIndices[key] = zipIndex;
                    }

                    var i = Interlocked.Increment(ref soFar);
                    updateProgress(DOWNLOAD_INDICES_PORTION * i / count);
                }
            )
            .ConfigureAwait(false);

        // Use the zip indices to read the raw entry data.
        List<(string Key, string EntryName)> entriesToDownload = [];
        foreach (var key in keys)
        {
            entriesToDownload.Add((key, ""));
            entriesToDownload.Add((key, "movie.m3u8"));
            entriesToDownload.Add((key, "clip.mp4"));
        }
        object rawDataLock = new();
        Dictionary<(string Key, string EntryName), string> rawData = []; // => FilePath
        count = entriesToDownload.Count;
        soFar = 0;
        await Parallel
            .ForEachAsync(
                entriesToDownload,
                new ParallelOptions { MaxDegreeOfParallelism = 64, CancellationToken = cancel },
                async (x, cancel) =>
                {
                    var zipIndex = zipIndices[x.Key];
                    var filePath = NewFilePath();
                    await GetRawDataAsync(x.Key, zipIndex, x.EntryName, filePath, cancel).ConfigureAwait(false);

                    lock (rawDataLock)
                    {
                        rawData[x] = filePath;
                    }

                    var i = Interlocked.Increment(ref soFar);
                    updateProgress(DOWNLOAD_INDICES_PORTION + DOWNLOAD_DATA_PORTION * i / count);
                }
            )
            .ConfigureAwait(false);

        // Extract/decrypt the entries.
        ConcurrentBag<EntryData> entries = [];
        count = keys.Count;
        soFar = 0;
        Parallel.ForEach(
            keys,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount - 1, CancellationToken = cancel },
            key =>
            {
                var zipIndex = zipIndices[key];

                // For all entries except movie.m3u8 and clip.mp4, include range but no data.
                foreach (var entry in zipIndex.Entries)
                {
                    if (entry.Name is "movie.m3u8" or "clip.mp4")
                        continue;

                    entries.Add(new(key, entry.Name, entry.OffsetLength, null));
                }

                var zipHeaderRawFilePath = rawData[(key, "")];
                var zipHeaderRawData = File.ReadAllBytes(zipHeaderRawFilePath);
                File.Delete(zipHeaderRawFilePath);
                var zipHeaderOffsetLength = zipIndex.ZipHeader;
                var zipFileLength = zipHeaderOffsetLength.Offset + zipHeaderOffsetLength.Length;

                var m3u8RawFilePath = rawData[(key, "movie.m3u8")];
                var m3u8RawData = File.ReadAllBytes(m3u8RawFilePath);
                File.Delete(m3u8RawFilePath);
                var m3u8OffsetLength = zipIndex.Entries.Single(x => x.Name == "movie.m3u8").OffsetLength;

                var clipRawFilePath = rawData[(key, "clip.mp4")];
                var clipRawData = File.ReadAllBytes(clipRawFilePath);
                File.Delete(clipRawFilePath);
                var clipOffsetLength = zipIndex.Entries.Single(x => x.Name == "clip.mp4").OffsetLength;

                // Patch together a sparse stream.
                List<SparseDataStream.Range> ranges =
                [
                    new(0, [0x50, 0x4B, 0x03, 0x04]),
                    new(zipHeaderOffsetLength.Offset, zipHeaderRawData),
                    new(m3u8OffsetLength.Offset, m3u8RawData),
                    new(clipOffsetLength.Offset, clipRawData),
                ];
                using SparseDataStream sparseStream = new(zipFileLength, ranges);

                // Extract the files.
                var zipHeaderFilePath = NewFilePath();
                File.WriteAllBytes(zipHeaderFilePath, zipHeaderRawData);
                entries.Add(new(key, "", zipHeaderOffsetLength, zipHeaderFilePath));

                var m3u8FilePath = NewFilePath();
                using (var extractedStream = File.Create(m3u8FilePath))
                {
                    EncryptedZipFile.ReadEntry(sparseStream, extractedStream, "movie.m3u8", password);
                    entries.Add(new(key, "movie.m3u8", m3u8OffsetLength, m3u8FilePath));
                }

                var clipFilePath = NewFilePath();
                using (var extractedStream = File.Create(clipFilePath))
                {
                    EncryptedZipFile.ReadEntry(sparseStream, extractedStream, "clip.mp4", password);
                    entries.Add(new(key, "clip.mp4", clipOffsetLength, clipFilePath));
                }

                var i = Interlocked.Increment(ref soFar);
                updateProgress(DOWNLOAD_INDICES_PORTION + DOWNLOAD_DATA_PORTION + EXTRACT_PORTION * i / count);
            }
        );

        return [.. entries];
    }

    private async Task<ZipIndex> ReadZipIndexAsync(string key, CancellationToken cancel)
    {
        // Get the last 100KB of the file, hoping that the zip index is fully contained within.
        var fileLength = await GetObjectLengthAsync(key, cancel).ConfigureAwait(false);
        var requestOffset = Math.Max(0, fileLength - 100_000);
        var requestLength = (int)(fileLength - requestOffset);
        var trailer = await GetByteRangeAsync(key, new(requestOffset, requestLength), cancel).ConfigureAwait(false);

        // The very last 8 bytes in the file are an Int64 offset to the start of the zip index.
        var zipIndexOffset = BitConverter.ToInt64(trailer, trailer.Length - 8);
        var zipIndexLength = (int)(fileLength - zipIndexOffset - 8);

        // Did the zip index fit in the 100KB we requested?
        if (zipIndexOffset < requestOffset)
        {
            // Nope. We have to request more data. Request starting at zipIndexOffset.
            requestOffset = zipIndexOffset;
            requestLength = (int)(fileLength - requestOffset);
            trailer = await GetByteRangeAsync(key, new(requestOffset, requestLength), cancel).ConfigureAwait(false);
        }

        // Parse all but the last 8 bytes as JSON.
        var jsonBytes = trailer.AsMemory((int)(zipIndexOffset - requestOffset), zipIndexLength);
        var jsonString = Encoding.UTF8.GetString(jsonBytes.Span);
        var zipIndex = JsonSerializer.Deserialize<ZipIndex>(jsonString);

        return zipIndex;
    }

    private async Task<long> GetObjectLengthAsync(string key, CancellationToken cancel)
    {
        return await _policy
            .ExecuteAsync(async () =>
            {
                var meta = await s3.GetObjectMetadataAsync(bucket, key, cancel).ConfigureAwait(false);
                return meta.ContentLength;
            })
            .ConfigureAwait(false);
    }

    private async Task GetByteRangeAsync(
        string key,
        OffsetLength offsetLength,
        string outFilePath,
        CancellationToken cancel
    )
    {
        await _policy
            .ExecuteAsync(async () =>
            {
                GetObjectRequest request =
                    new()
                    {
                        BucketName = bucket,
                        Key = key,
                        ByteRange = new(offsetLength.Offset, offsetLength.Offset + offsetLength.Length - 1),
                    };

                using var response = await s3.GetObjectAsync(request).ConfigureAwait(false);
                using var responseStream = response.ResponseStream;
                using var fileStream = File.Create(outFilePath);
                await responseStream.CopyToAsync(fileStream, cancel).ConfigureAwait(false);
            })
            .ConfigureAwait(false);
    }

    private async Task<byte[]> GetByteRangeAsync(string key, OffsetLength offsetLength, CancellationToken cancel)
    {
        return await _policy
            .ExecuteAsync(async () =>
            {
                GetObjectRequest request =
                    new()
                    {
                        BucketName = bucket,
                        Key = key,
                        ByteRange = new(offsetLength.Offset, offsetLength.Offset + offsetLength.Length - 1),
                    };

                using var response = await s3.GetObjectAsync(request).ConfigureAwait(false);
                using var responseStream = response.ResponseStream;
                using MemoryStream memoryStream = new();
                await responseStream.CopyToAsync(memoryStream, cancel).ConfigureAwait(false);
                return memoryStream.ToArray();
            })
            .ConfigureAwait(false);
    }

    private async Task GetRawDataAsync(
        string key,
        ZipIndex zipIndex,
        string entryName,
        string outFilePath,
        CancellationToken cancel
    )
    {
        OffsetLength offsetLength;
        if (entryName == "")
        {
            offsetLength = zipIndex.ZipHeader;
        }
        else
        {
            var entry = zipIndex.Entries.Single(x => x.Name == entryName);
            offsetLength = entry.OffsetLength;
        }

        await GetByteRangeAsync(key, offsetLength, outFilePath, cancel).ConfigureAwait(false);
    }
}
