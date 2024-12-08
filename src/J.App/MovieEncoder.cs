using System.Text.Json;
using System.Windows;
using J.Core;
using J.Core.Data;

namespace J.App;

public sealed class MovieEncoder(AccountSettingsProvider accountSettingsProvider, ProcessTempDir processTempDir)
{
    private const string PREFIX = "movie";

    public void Encode(
        string movieFilePath,
        TimeSpan movieDuration,
        string clipFilePath,
        string outZipFilePath,
        string outM3u8FilePath,
        ImportProgress importProgress,
        out ZipIndex zipIndex,
        CancellationToken cancel
    )
    {
        var password = accountSettingsProvider.Current.Password;
        using var tempDir = processTempDir.NewDir();
        var m3u8Path = Path.Combine(tempDir.Path, $"{PREFIX}.m3u8");
        var title = Path.GetFileNameWithoutExtension(movieFilePath);

        // HLS time:
        // There seems to be an issue with too many segments so we need to keep it reasonable.
        // 5 seconds at 1 hour 15 minutes seems to be around the cutoff for breakage.
        // So we'll do half that: 5 seconds per 45 minutes.
        var hlsTime = 5 * (1 + (int)(movieDuration.TotalMinutes / 45));

        importProgress.UpdateProgress(ImportProgress.Phase.Segmenting, 0);
        var (exitCode, log) = Ffmpeg.Run(
            $"-i \"{movieFilePath}\" -sn -metadata title=\"{title}\" -codec copy -start_number 0 -hls_time {hlsTime} -hls_list_size 0 -hls_playlist_type vod -f hls -hide_banner -loglevel error -progress pipe:1 \"{m3u8Path}\"",
            output =>
            {
                if (output.StartsWith("out_time=") && TimeSpan.TryParse(output.Split('=')[1].Trim(), out var time))
                {
                    importProgress.UpdateProgress(ImportProgress.Phase.Segmenting, time / movieDuration);
                }
            },
            cancel
        );
        if (exitCode != 0)
        {
            throw new Exception(
                $"Failed to encode \"{Path.GetFileName(movieFilePath)}\". FFmpeg failed with exit code {exitCode}.\n\nFFmpeg output:\n{log}"
            );
        }

        // We now have movie.m3u8 and movie0.ts, movie1.ts, etc.
        // Make a copy of the .m3u8 for the caller before we encrypt it.
        cancel.ThrowIfCancellationRequested();
        File.Copy(m3u8Path, outM3u8FilePath, overwrite: true);

        // Include the caller-provided clip.
        cancel.ThrowIfCancellationRequested();
        File.Copy(clipFilePath, Path.Combine(tempDir.Path, "clip.mp4"));

        // Include metadata about the original file.
        cancel.ThrowIfCancellationRequested();
        FileInfo sourceFileInfo = new(movieFilePath);
        SourceFileMetadata meta =
            new(
                Path.GetFileName(movieFilePath),
                sourceFileInfo.Length,
                sourceFileInfo.CreationTimeUtc,
                sourceFileInfo.LastWriteTimeUtc
            );
        File.WriteAllText(Path.Combine(tempDir.Path, "meta.json"), JsonSerializer.Serialize(meta));

        // Make an archive file.
        cancel.ThrowIfCancellationRequested();
        EncryptedZipFile.CreateMovieZip(outZipFilePath, tempDir.Path, password, importProgress, out zipIndex, cancel);
    }

    private readonly record struct SourceFileMetadata(
        string Filename,
        long Length,
        DateTime CreationTimeUtc,
        DateTime LastWriteTimeUtc
    );
}
