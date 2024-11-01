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

        ProcessStartInfo psi =
            new()
            {
                FileName = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe"),
                Arguments =
                    $"-i \"{movieFilePath}\" -metadata title=\"{title}\" -codec copy -start_number 0 -hls_time 5 -hls_list_size 0 -hls_playlist_type vod -f hls \"{m3u8Path}\"",
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
