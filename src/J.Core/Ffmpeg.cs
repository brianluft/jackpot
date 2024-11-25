using System.Diagnostics;
using System.Text.Json;
using J.Base;

namespace J.Core;

public static class Ffmpeg
{
    private const int LOG_LINES = 15;

    public readonly record struct Result(int ExitCode, string Log);

    public static Result Run(string arguments, CancellationToken cancel) =>
        Run(arguments, null, null, LOG_LINES, cancel);

    public static Result Run(string arguments, Action<string> outputCallback, CancellationToken cancel) =>
        Run(arguments, outputCallback, null, LOG_LINES, cancel);

    public static Result Probe(string arguments, CancellationToken cancel) =>
        Run(arguments, null, "ffprobe.exe", 1_000_000, cancel);

    public static Result Run(
        string arguments,
        Action<string>? outputCallback,
        string? filename,
        CancellationToken cancel
    ) => Run(arguments, outputCallback, filename, LOG_LINES, cancel);

    public static Result Run(
        string arguments,
        Action<string>? outputCallback,
        string? filename,
        int maxLogLines,
        CancellationToken cancel
    )
    {
        cancel.ThrowIfCancellationRequested();

        ProcessStartInfo psi =
            new()
            {
#if DEBUG
                // For debug builds, use ffmpeg.exe in PATH since the ffmpeg install gets inserted only for releases.
                FileName = filename ?? "ffmpeg.exe",
#else
                FileName = Path.Combine(AppContext.BaseDirectory, "ffmpeg", filename ?? "ffmpeg.exe"),
#endif
                Arguments = arguments,
                WorkingDirectory = "C:\\",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

        using var p = Process.Start(psi)!;
        object logLock = new();
        Queue<string> log = [];

        p.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                lock (logLock)
                {
                    while (log.Count >= LOG_LINES - 1)
                        log.Dequeue();
                    log.Enqueue(e.Data);
                }
                outputCallback?.Invoke(e.Data);
            }
        };
        p.BeginOutputReadLine();

        p.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                lock (logLock)
                {
                    while (log.Count >= LOG_LINES - 1)
                        log.Dequeue();
                    log.Enqueue(e.Data);
                }
            }
        };
        p.BeginErrorReadLine();

        ApplicationSubProcesses.Add(p);

        while (!p.WaitForExit(20))
        {
            if (cancel.IsCancellationRequested)
            {
                try
                {
                    p.Kill();
                }
                catch { }

                return new(-1, string.Join("\n", log));
            }
        }

        p.WaitForExit();

        return new(p.ExitCode, string.Join("\n", log));
    }

    public static bool IsCompatibleCodec(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            var arguments = $"-v error -select_streams v:0 -show_entries stream=codec_name -of json -i \"{filePath}\"";
            var result = Probe(arguments, CancellationToken.None);

            if (result.ExitCode != 0)
                return false;

            using var doc = JsonDocument.Parse(result.Log);
            var streams = doc.RootElement.GetProperty("streams");

            if (streams.GetArrayLength() == 0)
                return false;

            var codecName = streams[0].GetProperty("codec_name").GetString()?.ToLowerInvariant();
            if (codecName is not "h264" or "hevc")
                Debugger.Break();
            return codecName is "h264" or "hevc";
        }
        catch
        {
            return false;
        }
    }
}
