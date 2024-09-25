using Polly;
using Polly.Retry;

namespace J.Server;

public static class ServerPolicy
{
    public static AsyncRetryPolicy Policy { get; } = Polly.Policy.Handle<Exception>().RetryAsync(5);
}
