using J.Core;

namespace J.App;

public static class MovieConverter
{
    public static void Convert(
        string inFilePath,
        string outFilePath,
        int videoCrf,
        string videoPreset,
        int audioBitrate,
        Action<double> updateProgress,
        CancellationToken cancel
    )
    {
        var duration = Ffmpeg.GetMovieDuration(inFilePath, cancel);
        cancel.ThrowIfCancellationRequested();

        var arguments =
            $"-i \"{inFilePath}\" -c:v libx264 -preset \"{videoPreset}\" -crf \"{videoCrf}\" -pix_fmt yuv420p -c:a aac -b:a {audioBitrate}k -threads {Environment.ProcessorCount - 1} -movflags +faststart -hide_banner -loglevel error -progress pipe:1 -y \"{outFilePath}\"";

        var (exitCode, log) = Ffmpeg.Run(
            arguments,
            output =>
            {
                if (output.StartsWith("out_time="))
                {
                    var time = TimeSpan.Parse(output.Split('=')[1].Trim());
                    updateProgress(time / duration);
                }
            },
            cancel
        );

        cancel.ThrowIfCancellationRequested();

        if (exitCode != 0)
        {
            throw new Exception(
                $"Failed to convert \"{Path.GetFileName(inFilePath)}\". FFmpeg failed with exit code {exitCode}.\n\nFFmpeg output:\n{log}"
            );
        }

        if (!File.Exists(outFilePath))
        {
            throw new Exception(
                $"Failed to convert \"{Path.GetFileName(inFilePath)}\". FFmpeg did not produce the output file.\n\nFFmpeg output:\n{log}"
            );
        }
    }
}
