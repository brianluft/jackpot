using System.Diagnostics;
using J.Base;

namespace J.Core;

public static class Ffmpeg
{
    private const int LOG_LINES = 15;

    public readonly record struct Result(int ExitCode, string Log);

    public static Result Run(string arguments, CancellationToken cancel) => Run(arguments, null, null, cancel);

    public static Result Run(string arguments, Action<string> outputCallback, CancellationToken cancel) =>
        Run(arguments, outputCallback, null, cancel);

    public static Result Run(
        string arguments,
        Action<string>? outputCallback,
        string? filename,
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

        return new(p.ExitCode, string.Join("\n", log));
    }
}
