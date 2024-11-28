using Amazon.S3;
using J.Core;
using J.Core.Data;

namespace J.App;

public sealed class Importer(
    AccountSettingsProvider accountSettingsProvider,
    ProcessTempDir processTempDir,
    MovieEncoder movieEncoder,
    LibraryProviderAdapter libraryProvider,
    S3Uploader s3Uploader
) : IDisposable
{
    private static readonly Lock _libraryUpdateLock = new();
    private readonly IAmazonS3 _s3 = accountSettingsProvider.CreateAmazonS3Client();

    public void Dispose()
    {
        _s3.Dispose();
    }

    public void Import(string sourceFilePath, ImportProgress importProgress, CancellationToken cancel)
    {
        var password = accountSettingsProvider.Current.Password;

        using var dir = processTempDir.NewDir();
        var encodedFullMovieFilePath = Path.Combine(dir.Path, "full.zip");
        var clipFilePath = Path.Combine(dir.Path, "clip.mp4");
        var m3u8FilePath = Path.Combine(dir.Path, "movie.m3u8");

        importProgress.UpdateMessage("Scanning");
        var duration = Ffmpeg.GetMovieDuration(sourceFilePath, cancel);
        cancel.ThrowIfCancellationRequested();

        importProgress.UpdateMessage("Waiting");
        lock (GlobalLocks.BigCpu)
        {
            importProgress.UpdateProgress(ImportProgress.Phase.MakingThumbnails, 0);
            MakeClip(sourceFilePath, duration, clipFilePath, importProgress, cancel);
            cancel.ThrowIfCancellationRequested();
        }

        importProgress.UpdateProgress(ImportProgress.Phase.Encrypting, 0);
        movieEncoder.Encode(
            sourceFilePath,
            duration,
            clipFilePath,
            encodedFullMovieFilePath,
            m3u8FilePath,
            importProgress,
            out var zipIndex,
            cancel
        );
        cancel.ThrowIfCancellationRequested();

        importProgress.UpdateProgress(ImportProgress.Phase.Uploading, 0);
        UploadEncodedFullMovie(
            encodedFullMovieFilePath,
            progress =>
            {
                importProgress.UpdateProgress(ImportProgress.Phase.Uploading, progress);
            },
            out var s3Key,
            cancel
        );

        Movie movie =
            new(
                Id: new MovieId(),
                Filename: Path.GetFileNameWithoutExtension(sourceFilePath),
                S3Key: s3Key,
                DateAdded: DateTimeOffset.Now
            );

        MovieFile zipHeaderFile =
            new(movie.Id, "", zipIndex.ZipHeader, ReadFileByteRange(encodedFullMovieFilePath, zipIndex.ZipHeader));

        var m3u8Entry = zipIndex.Entries.Single(x => x.Name == "movie.m3u8");
        MovieFile m3u8File =
            new(
                movie.Id,
                "movie.m3u8",
                m3u8Entry.OffsetLength,
                ReadEntry(encodedFullMovieFilePath, "movie.m3u8", password)
            );

        var clipEntry = zipIndex.Entries.Single(x => x.Name == "clip.mp4");
        MovieFile clipFile =
            new(
                movie.Id,
                "clip.mp4",
                clipEntry.OffsetLength,
                ReadEntry(encodedFullMovieFilePath, "clip.mp4", password)
            );

        var otherFiles = zipIndex
            .Entries.Where(x => x.Name != "movie.m3u8" && x.Name != "clip.mp4")
            .Select(x => new MovieFile(movie.Id, x.Name, x.OffsetLength, null))
            .ToList();

        List<MovieFile> files = [zipHeaderFile, m3u8File, clipFile, .. otherFiles];
        lock (_libraryUpdateLock)
        {
            libraryProvider.NewMovieAsync(movie, files, _ => { }, cancel).GetAwaiter().GetResult();
        }
    }

    private static byte[] ReadFileByteRange(string filePath, OffsetLength offsetLength)
    {
        using var fs = File.OpenRead(filePath);
        fs.Position = offsetLength.Offset;
        var buffer = new byte[offsetLength.Length];
        fs.ReadExactly(buffer);
        return buffer;
    }

    private static byte[] ReadEntry(string zipFilePath, string entryName, Password password)
    {
        using var fs = File.OpenRead(zipFilePath);
        using MemoryStream ms = new();
        EncryptedZipFile.ReadEntry(fs, ms, entryName, password);
        return ms.ToArray();
    }

    private void MakeClip(
        string sourceFilePath,
        TimeSpan sourceDuration,
        string outFilePath,
        ImportProgress importProgress,
        CancellationToken cancel
    )
    {
        importProgress.UpdateProgress(ImportProgress.Phase.MakingThumbnails, 0);

        List<TimeSpan> clipStartTimes = [];

        if (sourceDuration < TimeSpan.FromSeconds(10))
        {
            clipStartTimes.Add(TimeSpan.Zero);
        }
        else
        {
            clipStartTimes.Add(0.225 * sourceDuration);
            clipStartTimes.Add(0.3 * sourceDuration);
            clipStartTimes.Add(0.375 * sourceDuration);
            clipStartTimes.Add(0.45 * sourceDuration);
            clipStartTimes.Add(0.525 * sourceDuration);
            clipStartTimes.Add(0.6 * sourceDuration);
            clipStartTimes.Add(0.675 * sourceDuration);
            clipStartTimes.Add(0.75 * sourceDuration);
            clipStartTimes.Add(0.825 * sourceDuration);
            clipStartTimes.Add(0.9 * sourceDuration);
        }

        using var dir = processTempDir.NewDir();
        var clipFilenames = new string[clipStartTimes.Count];

        var count = clipStartTimes.Count + 1; // extra one for the final encoding
        var soFar = 0; // interlocked

        Parallel.For(
            0,
            clipStartTimes.Count,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) },
            i =>
            {
                string clipFilename = $"clip{i}.mov";
                var clipFilePath = Path.Combine(dir.Path, clipFilename);
                MakeSingleClip(sourceFilePath, clipFilePath, clipStartTimes[i], TimeSpan.FromSeconds(1), cancel);
                clipFilenames[i] = clipFilename;

                var progress = (double)Interlocked.Increment(ref soFar) / count;
                importProgress.UpdateProgress(ImportProgress.Phase.MakingThumbnails, progress);
            }
        );

        ConcatenateClipsAndEncodeAsH264(dir.Path, clipFilenames, outFilePath, cancel);
        importProgress.UpdateProgress(ImportProgress.Phase.MakingThumbnails, 1);
    }

    private static void MakeSingleClip(
        string sourceFilePath,
        string outFilePath,
        TimeSpan offset,
        TimeSpan length,
        CancellationToken cancel
    )
    {
        RunFfmpeg(
            $"-ss {offset.TotalSeconds} -i \"{sourceFilePath}\" -t {length.TotalSeconds} -vf \"scale=-2:432\" -r 30 -c:v prores -profile:v 3 -pix_fmt yuv422p10le -an -threads {Math.Max(1, Environment.ProcessorCount - 1)} \"{outFilePath}\"",
            cancel
        );
    }

    private static void ConcatenateClipsAndEncodeAsH264(
        string dir,
        string[] clipFilenames,
        string outFilePath,
        CancellationToken cancel
    )
    {
        File.WriteAllLines(Path.Combine(dir, "file_list.txt"), clipFilenames.Select(f => $"file '{f}'"));

        RunFfmpeg(
            $"-f concat -safe 0 -i \"{Path.Combine(dir, "file_list.txt")}\" -c:v libx264 -crf 25 -pix_fmt yuv420p -preset veryslow -an -movflags +faststart -threads {Math.Max(1, Environment.ProcessorCount - 1)} \"{outFilePath}\"",
            cancel
        );
    }

    private static void RunFfmpeg(string arguments, CancellationToken cancel)
    {
        var (exitCode, log) = Ffmpeg.Run(arguments, cancel);
        if (exitCode != 0)
            throw new Exception($"FFmpeg failed with exit code: {exitCode}\n\nFFmpeg output: {log}");
    }

    private void UploadEncodedFullMovie(
        string encodedFilePath,
        Action<double> updateProgress,
        out string outS3Key,
        CancellationToken cancel
    )
    {
        var guid = Guid.NewGuid().ToString();
        var s3Key = outS3Key = $"content/{guid[..2]}/{guid}.zip";

        s3Uploader.PutObject(encodedFilePath, accountSettingsProvider.Current.Bucket, s3Key, updateProgress, cancel);
    }
}
