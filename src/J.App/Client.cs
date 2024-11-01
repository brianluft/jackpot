using System.Diagnostics;
using System.Net.Http.Json;
using System.Web;
using J.Base;
using J.Core;
using J.Core.Data;

namespace J.App;

public sealed class Client(IHttpClientFactory httpClientFactory) : IDisposable
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(typeof(Client).FullName!);

    private readonly object _lock = new();
    private Process? _process;

    public void Start()
    {
        lock (_lock)
        {
            //if (_process is not null)
            //throw new InvalidOperationException("The web server is already running.");

            var dir = Path.GetDirectoryName(typeof(Client).Assembly.Location!)!;
            var exe = Path.Combine(dir, "Jackpot.Server.exe");

            ProcessStartInfo psi =
                new()
                {
                    FileName = exe,
                    WorkingDirectory = dir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

            _process = Process.Start(psi)!;
            ApplicationSubProcesses.Add(_process);
        }
    }

    public void Dispose()
    {
        Stop();
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_process is not null)
            {
                _process.Kill();
                _process.Dispose();
                _process = null;
            }
        }
    }

    public string GetMovieM3u8Url(Movie movie)
    {
        var query = HttpUtility.ParseQueryString("");
        query["movieId"] = movie.Id.Value;
        return $"http://localhost:6786/movie.m3u8?{query}";
    }

    public async Task RefreshLibraryAsync(CancellationToken cancel)
    {
        await _httpClient
            .PostAsync("http://localhost:6786/refresh-library", new StringContent(""), cancel)
            .ConfigureAwait(false);
    }

    public async Task SetShuffleAsync(bool shuffle, CancellationToken cancel)
    {
        var query = HttpUtility.ParseQueryString("");
        query["on"] = shuffle.ToString();
        await _httpClient
            .PostAsync($"http://localhost:6786/shuffle?{query}", new StringContent(""), cancel)
            .ConfigureAwait(false);
    }

    public async Task SetFilterAsync(Filter filter, CancellationToken cancel)
    {
        await _httpClient.PostAsJsonAsync("http://localhost:6786/filter", filter, cancel).ConfigureAwait(false);
    }
}
