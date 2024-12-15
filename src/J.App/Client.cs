using System.Diagnostics;
using System.Web;
using J.Core;
using J.Core.Data;
using Polly;
using Polly.Timeout;

namespace J.App;

public sealed class Client(IHttpClientFactory httpClientFactory, Preferences preferences) : IDisposable
{
    private const int MAX_LOG_LINES = 1000;
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(typeof(Client).FullName!);

    private static readonly AsyncTimeoutPolicy _policy = Policy.TimeoutAsync(TimeSpan.FromSeconds(10));

    private readonly Lock _processLock = new();
    private Process? _process;

    private readonly Lock _logLock = new();
    private readonly Queue<string> _log = [];

    public int Port { get; } = 777;
    public string SessionPassword { get; } = Guid.NewGuid().ToString();

    public void Start()
    {
        lock (_processLock)
        {
            if (_process is not null)
                throw new InvalidOperationException("The web server is already running.");

            var dir = Path.GetDirectoryName(typeof(Client).Assembly.Location!)!;
            var exe = Path.Combine(dir, "Jackpot.Server.exe");

            ProcessStartInfo psi =
                new()
                {
                    FileName = exe,
                    WorkingDirectory = dir,
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };

            var anyNetworkSharing =
                preferences.GetBoolean(Preferences.Key.NetworkSharing_AllowVlcAccess)
                || preferences.GetBoolean(Preferences.Key.NetworkSharing_AllowWebBrowserAccess);

            var bindHost = anyNetworkSharing ? "*" : "localhost";
            psi.Environment["ASPNETCORE_URLS"] = $"http://{bindHost}:{Port}";
            psi.Environment["JACKPOT_SESSION_PASSWORD"] = SessionPassword;

            _process = Process.Start(psi)!;
            ApplicationSubProcesses.Add(_process);
            PowerThrottlingUtil.DisablePowerThrottling(_process);

            _process.OutputDataReceived += Process_DataReceived;
            _process.BeginOutputReadLine();
            _process.ErrorDataReceived += Process_DataReceived;
            _process.BeginErrorReadLine();
        }
    }

    private void Process_DataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        lock (_logLock)
        {
            while (_log.Count >= MAX_LOG_LINES)
                _log.Dequeue();

            _log.Enqueue(e.Data!);
        }
    }

    public List<string> GetLog()
    {
        lock (_logLock)
        {
            return new(_log);
        }
    }

    public void Dispose()
    {
        Stop();
    }

    public void Stop()
    {
        lock (_processLock)
        {
            if (_process is not null)
            {
                _process.Kill();
                _process.Dispose();
                _process = null;
            }
        }
    }

    public void Restart()
    {
        lock (_processLock)
        {
            Stop();
            Start();
        }
    }

    public string GetMoviePlayerUrl(MovieId movieId)
    {
        var query = HttpUtility.ParseQueryString("");
        query["sessionPassword"] = SessionPassword;
        query["movieId"] = movieId.Value;
        return $"http://localhost:{Port}/movie-play.html?{query}";
    }

    public async Task ReshuffleAsync()
    {
        await DoAsync(async cancel =>
            {
                var query = HttpUtility.ParseQueryString("");
                query["sessionPassword"] = SessionPassword;
                var response = await _httpClient
                    .PostAsync($"http://localhost:{Port}/reshuffle?{query}", null, cancel)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            })
            .ConfigureAwait(false);
    }

    public async Task InhibitScrollRestoreAsync()
    {
        await DoAsync(async cancel =>
            {
                var query = HttpUtility.ParseQueryString("");
                query["sessionPassword"] = SessionPassword;
                var response = await _httpClient
                    .PostAsync($"http://localhost:{Port}/inhibit-scroll-restore?{query}", null, cancel)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            })
            .ConfigureAwait(false);
    }

    private async Task DoAsync(Func<CancellationToken, Task> func)
    {
        try
        {
            await _policy.ExecuteAsync(func, default).ConfigureAwait(false);
        }
        catch (TimeoutRejectedException)
        {
            throw new Exception("Jackpot's internal server is not responding. Please restart the application.");
        }
    }
}
