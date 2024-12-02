namespace J.Server;

public sealed class RequestIdProvider(IHttpContextAccessor httpContextAccessor)
{
    public string Id => httpContextAccessor.HttpContext!.Items["RequestId"]!.ToString()!;
}
