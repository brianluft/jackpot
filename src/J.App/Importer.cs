using System.Diagnostics;
using Amazon.S3;
using J.Base;
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
    private static readonly object _cpuLock = new();
    private readonly IAmazonS3 _s3 = accountSettingsProvider.CreateAmazonS3Client();

    public void Dispose()
    {
        _s3.Dispose();
    }

    public void Import(string sourceFilePath, CancellationToken cancel)
    {
        var password = accountSettingsProvider.Current.Password;

        using var dir = processTempDir.NewDir();
        var encodedFullMovieFilePath = Path.Combine(dir.Path, "full.zip");
        var clipFilePath = Path.Combine(dir.Path, "clip.mp4");
        var m3u8FilePath = Path.Combine(dir.Path, "movie.m3u8");

        ZipIndex zipIndex;
        lock (_cpuLock)
        {
            var duration = GetMovieDuration(sourceFilePath);

            MakeClip(sourceFilePath, duration, clipFilePath);
            cancel.ThrowIfCancellationRequested();

            movieEncoder.Encode(sourceFilePath, clipFilePath, encodedFullMovieFilePath, m3u8FilePath, out zipIndex);
            cancel.ThrowIfCancellationRequested();
        }

        UploadEncodedFullMovie(encodedFullMovieFilePath, out var s3Key, cancel);

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
        libraryProvider.NewMovieAsync(movie, files, _ => { }, cancel).GetAwaiter().GetResult();
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

    private TimeSpan GetMovieDuration(string filePath)
    {
        ProcessStartInfo psi =
            new()
            {
                FileName = "ffprobe",
                Arguments = $"-i \"{filePath}\" -show_entries format=duration -v quiet -of csv=\"p=0\"",
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                UseShellExecute = false,
            };

        using var p = Process.Start(psi)!;
        ApplicationSubProcesses.Add(p);

        TimeSpan? duration = null;
        p.OutputDataReceived += (sender, e) =>
        {
            if (e.Data is null)
                return;

            if (double.TryParse(e.Data.Trim(), out var seconds))
                duration = TimeSpan.FromSeconds(seconds);
        };

        p.BeginOutputReadLine();
        p.WaitForExit();

        if (duration is null)
            throw new Exception("Failed to parse movie duration from ffprobe output.");

        return duration.Value;
    }

    private void MakeClip(string sourceFilePath, TimeSpan sourceDuration, string outFilePath)
    {
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
        Parallel.For(
            0,
            clipStartTimes.Count,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) },
            i =>
            {
                string clipFilename = $"clip{i}.mov";
                var clipFilePath = Path.Combine(dir.Path, clipFilename);
                MakeSingleClip(sourceFilePath, clipFilePath, clipStartTimes[i], TimeSpan.FromSeconds(1));
                clipFilenames[i] = clipFilename;
            }
        );

        ConcatenateClipsAndEncodeAsH264(dir.Path, clipFilenames, outFilePath);
    }

    private static void MakeSingleClip(string sourceFilePath, string outFilePath, TimeSpan offset, TimeSpan length)
    {
        Ffmpeg(
            $"-ss {offset.TotalSeconds} -i \"{sourceFilePath}\" -t {length.TotalSeconds} -vf \"scale=-2:432\" -r 30 -c:v prores -profile:v 3 -pix_fmt yuv422p10le -an -threads {Math.Max(1, Environment.ProcessorCount - 1)} \"{outFilePath}\""
        );
    }

    private static void ConcatenateClipsAndEncodeAsH264(string dir, string[] clipFilenames, string outFilePath)
    {
        File.WriteAllLines(Path.Combine(dir, "file_list.txt"), clipFilenames.Select(f => $"file '{f}'"));

        Ffmpeg(
            $"-f concat -safe 0 -i \"{Path.Combine(dir, "file_list.txt")}\" -c:v libx264 -crf 25 -pix_fmt yuv420p -preset veryslow -an -movflags +faststart -threads {Math.Max(1, Environment.ProcessorCount - 1)} \"{outFilePath}\""
        );
    }

    private static void Ffmpeg(string arguments)
    {
        ProcessStartInfo psi =
            new()
            {
                FileName = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe"),
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
            };

        using var p = Process.Start(psi)!;
        ApplicationSubProcesses.Add(p);
        p.WaitForExit();
        if (p.ExitCode != 0)
            throw new Exception($"ffmpeg failed with exit code: {p.ExitCode}");
    }

    private void UploadEncodedFullMovie(string encodedFilePath, out string outS3Key, CancellationToken cancel)
    {
        var guid = Guid.NewGuid().ToString();
        var s3Key = outS3Key = $"content/{guid[..2]}/{guid}.zip";

        s3Uploader.PutObject(encodedFilePath, accountSettingsProvider.Current.Bucket, s3Key, cancel);
    }
}
