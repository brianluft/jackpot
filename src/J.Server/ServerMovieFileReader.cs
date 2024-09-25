using Amazon.S3;
using Amazon.S3.Model;
using J.Core;
using J.Core.Data;

namespace J.Server;

public sealed class ServerMovieFileReader(LibraryProvider libraryProvider, IAmazonS3 s3)
{
    public async Task ReadM3u8Async(MovieId movieId, Stream output, CancellationToken cancel)
    {
        var bytes = libraryProvider.GetM3u8(movieId);
        await output.WriteAsync(bytes, cancel);
    }

    public void ReadTs(MovieId movieId, int tsIndex, string bucket, Password password, Stream output)
    {
        var entryName = $"movie{tsIndex}.ts";

        // Get the content of the .zip header and its location in the .zip file.
        var movie = libraryProvider.GetMovie(movieId);
        var (zipHeaderOffset, zipHeaderData) = libraryProvider.GetZipHeader(movieId);
        var zipFileLength = zipHeaderOffset + zipHeaderData.Length;

        // Get the content of the entry (encrypted!) and its location in the .zip file.
        var tsEntryOffsetLength = libraryProvider.GetMovieFileOffsetLength(movieId, entryName);
        S3Path path = new(bucket, movie.S3Key);
        var tsEntryData = GetByteRange(path, tsEntryOffsetLength);

        // Patch together a sparse stream from these two ranges.
        List<SparseDataStream.Range> ranges =
        [
            new(0, [0x50, 0x4B, 0x03, 0x04]),
            new(zipHeaderOffset, zipHeaderData),
            new(tsEntryOffsetLength.Offset, tsEntryData),
        ];
        using SparseDataStream sparseStream = new(zipFileLength, ranges);

        // Extract the file.
        EncryptedZipFile.ReadEntry(sparseStream, output, entryName, password);
    }

    private byte[] GetByteRange(S3Path path, OffsetLength offsetLength)
    {
        return ServerPolicy
            .Policy.ExecuteAsync(async () =>
            {
                GetObjectRequest request =
                    new()
                    {
                        BucketName = path.Bucket,
                        Key = path.Key,
                        ByteRange = new(offsetLength.Offset, offsetLength.Offset + offsetLength.Length - 1),
                    };

                using var response = await s3.GetObjectAsync(request);
                using var stream = response.ResponseStream;
                using MemoryStream ms = new();
                stream.CopyTo(ms);
                return ms.ToArray();
            })
            .GetAwaiter()
            .GetResult();
    }
}
