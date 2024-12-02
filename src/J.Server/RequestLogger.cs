namespace J.Server;

public sealed class RequestLogger(ILogger<RequestLogger> logger, RequestIdProvider requestIdProvider)
{
    public void Log(string message)
    {
        logger.LogInformation("[{RequestId}] {Message}", requestIdProvider.Id, message);
    }
}
