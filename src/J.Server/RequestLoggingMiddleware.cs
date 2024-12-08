using System.Diagnostics;
using Microsoft.AspNetCore.Http.Extensions;

namespace J.Server;

public sealed class RequestLoggingMiddleware(ILogger<RequestLoggingMiddleware> logger) : IMiddleware
{
    private static int _requestId = 0; // interlocked

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Generate a unique ID for this request
        var requestId = Interlocked.Increment(ref _requestId).ToString();

        // Add the request ID to the HttpContext items so it can be accessed throughout the request pipeline
        context.Items["RequestId"] = requestId;

        // Add request ID to response headers
        context.Response.Headers["X-Request-ID"] = requestId;

        var sw = Stopwatch.StartNew();

        // Log the start of the request
        logger.LogInformation(
            "[#{RequestId}] [{Time}] Starting {Path}",
            requestId,
            DateTimeOffset.Now,
            context.Request.GetEncodedPathAndQuery()
        );

        try
        {
            // Call the next middleware in the pipeline
            await next(context);
        }
        finally
        {
            sw.Stop();

            // Log the completion of the request
            logger.LogInformation(
                "[#{RequestId}] [{Time}] Completed {Path} | Status: {StatusCode} | Duration: {Duration}ms",
                requestId,
                DateTimeOffset.Now,
                context.Request.GetEncodedPathAndQuery(),
                context.Response.StatusCode,
                sw.ElapsedMilliseconds
            );
        }
    }
}
