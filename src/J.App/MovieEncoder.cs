using System.Diagnostics;
using System.Text.Json;
using J.Base;
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
        out ZipIndex zipIndex
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

        ProcessStartInfo psi =
            new()
            {
#if DEBUG
                // For debug builds, use ffmpeg.exe in PATH since the ffmpeg install gets inserted only for releases.
                FileName = "ffmpeg.exe",
#else
                FileName = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe"),
#endif
                Arguments =
                    $"-i \"{movieFilePath}\" -metadata title=\"{title}\" -codec copy -start_number 0 -hls_time {hlsTime} -hls_list_size 0 -hls_playlist_type vod -f hls \"{m3u8Path}\"",
                WorkingDirectory = "",
                UseShellExecute = false,
                CreateNoWindow = true,
            };

        using (var p = Process.Start(psi)!)
        {
            ApplicationSubProcesses.Add(p);
            p.WaitForExit();
            if (p.ExitCode != 0)
                throw new Exception($"Failed to encode movie with ffmpeg. Exit code: {p.ExitCode}");
        }

        // We now have movie.m3u8 and movie0.ts, movie1.ts, etc.
        // Make a copy of the .m3u8 for the caller before we encrypt it.
        File.Copy(m3u8Path, outM3u8FilePath, overwrite: true);

        // Include the caller-provided clip.
        File.Copy(clipFilePath, Path.Combine(tempDir.Path, "clip.mp4"));

        // Include metadata about the original file.
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
        EncryptedZipFile.CreateMovieZip(outZipFilePath, tempDir.Path, password, out zipIndex);
    }

    private readonly record struct SourceFileMetadata(
        string Filename,
        long Length,
        DateTime CreationTimeUtc,
        DateTime LastWriteTimeUtc
    );
}
