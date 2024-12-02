using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Web;
using J.Base;
using J.Core;
using J.Core.Data;

namespace J.App;

public sealed class Client(IHttpClientFactory httpClientFactory, Preferences preferences) : IDisposable
{
    private const int MAX_LOG_LINES = 1000;
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(typeof(Client).FullName!);

    private readonly Lock _processLock = new();
    private Process? _process;

    private readonly Lock _logLock = new();
    private readonly Queue<string> _log = [];

    public int Port { get; private set; } = -1;
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

            Port = FindRandomUnusedPort();

            var m3u8Settings = preferences.GetJson<M3u8SyncSettings>(Preferences.Key.M3u8FolderSync_Settings);

            var bindHost = m3u8Settings.EnableLocalM3u8Folder ? "*" : "localhost";
            psi.Environment["ASPNETCORE_URLS"] = $"http://{bindHost}:{Port}";
            psi.Environment["JACKPOT_SESSION_PASSWORD"] = SessionPassword;

            _process = Process.Start(psi)!;

            _process.OutputDataReceived += Process_DataReceived;
            _process.BeginOutputReadLine();
            _process.ErrorDataReceived += Process_DataReceived;
            _process.BeginErrorReadLine();

            ApplicationSubProcesses.Add(_process);
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

    private static int FindRandomUnusedPort()
    {
        // Create a TCP/IP socket and bind to a random port assigned by the OS
        using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));

        // Get the assigned port number
        var port = ((IPEndPoint)socket.LocalEndPoint!).Port;
        if (port == 0)
            throw new Exception("Unable to find a port number.");

        return port;
    }

    public async Task ReshuffleAsync(CancellationToken cancel)
    {
        var query = HttpUtility.ParseQueryString("");
        query["sessionPassword"] = SessionPassword;
        var response = await _httpClient
            .PostAsync($"http://localhost:{Port}/reshuffle?{query}", null, cancel)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}
