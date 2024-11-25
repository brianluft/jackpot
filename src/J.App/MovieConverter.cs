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
        var totalFrames = GetFrameCount(inFilePath, cancel);
        var currentFrame = 0L;
        cancel.ThrowIfCancellationRequested();

        void UpdateFrame(int frame)
        {
            currentFrame = frame - 1;
            if (totalFrames.HasValue)
                updateProgress((double)currentFrame / totalFrames.Value);
        }

        var arguments =
            $"-i \"{inFilePath}\" -c:v libx264 -preset \"{videoPreset}\" -crf \"{videoCrf}\" -pix_fmt yuv420p -c:a aac -b:a {audioBitrate}k -threads {Environment.ProcessorCount - 1} -movflags +faststart -hide_banner -loglevel error -progress pipe:1 -y \"{outFilePath}\"";

        var (exitCode, log) = Ffmpeg.Run(
            arguments,
            output =>
            {
                if (output.StartsWith("frame="))
                {
                    var frame = int.Parse(output.Split('=')[1].Trim());
                    UpdateFrame(frame);
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

    private static int? GetFrameCount(string filePath, CancellationToken cancel)
    {
        int? frameCount = null;
        var na = false;

        var (exitCode, log) = Ffmpeg.Run(
            $"-v error -select_streams v:0 -show_entries stream=nb_frames -of default=noprint_wrappers=1:nokey=1 \"{filePath}\"",
            output =>
            {
                if (output.Trim().Equals("N/A", StringComparison.OrdinalIgnoreCase))
                    na = true;
                else if (int.TryParse(output.Trim(), out var frames))
                    frameCount = frames;
            },
            "ffprobe.exe",
            cancel
        );

        if (exitCode != 0)
        {
            throw new Exception(
                $"Failed to inspect \"{Path.GetFileName(filePath)}\". FFprobe failed with exit code {exitCode}.\n\nFFprobe output:\n{log}"
            );
        }

        if (na)
            return null;

        if (!frameCount.HasValue)
        {
            throw new Exception(
                $"Failed to inspect \"{Path.GetFileName(filePath)}\". FFprobe returned successfully, but did not produce the frame count.\n\nFFprobe output:\n{log}"
            );
        }

        return frameCount.Value;
    }
}
