using System.Diagnostics;
using Amazon.S3.Transfer;
using J.Core;
using J.Core.Data;

namespace J.App;

public sealed class MovieExporter(AccountSettingsProvider accountSettingsProvider, ProcessTempDir processTempDir)
{
    public void Export(Movie movie, string outFilePath, Action<double> updateProgress, CancellationToken cancel)
    {
        var password = accountSettingsProvider.Current.Password;

        using var dir = processTempDir.NewDir();
        var zipFilePath = Path.Combine(dir.Path, "movie.zip");
        DownloadZipFile(movie, updateProgress, zipFilePath, cancel);
        cancel.ThrowIfCancellationRequested();

        EncryptedZipFile.ExtractToDirectory(zipFilePath, dir.Path, password);

        var m3u8Path = Path.Combine(dir.Path, "movie.m3u8");
        ProcessStartInfo psi =
            new()
            {
                FileName = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe"),
                Arguments = $"-y -i \"{m3u8Path}\" -codec copy \"{outFilePath}\"",
                WorkingDirectory = "",
                UseShellExecute = false,
                CreateNoWindow = true,
            };

        using var p = Process.Start(psi)!;
        p.WaitForExit();
        if (p.ExitCode != 0)
            throw new Exception($"Failed to export movie with ffmpeg. Exit code: {p.ExitCode}");
    }

    private void DownloadZipFile(
        Movie movie,
        Action<double> updateProgress,
        string encFilePath,
        CancellationToken cancel
    )
    {
        using var s3 = accountSettingsProvider.CreateAmazonS3Client();

        using TransferUtility transferUtility = new(s3, new TransferUtilityConfig { ConcurrentServiceRequests = 4 });

        TransferUtilityDownloadRequest request =
            new()
            {
                BucketName = accountSettingsProvider.Current.Bucket,
                FilePath = encFilePath,
                Key = movie.S3Key,
            };

        request.WriteObjectProgressEvent += (sender, e) =>
        {
            updateProgress((double)e.TransferredBytes / e.TotalBytes);
        };

        transferUtility.DownloadAsync(request, cancel).GetAwaiter().GetResult();
    }
}
