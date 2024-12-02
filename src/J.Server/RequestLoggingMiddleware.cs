using System.Diagnostics;
using Microsoft.AspNetCore.Http.Extensions;

namespace J.Server;

public sealed class RequestLoggingMiddleware(ILogger<RequestLoggingMiddleware> logger) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Generate a unique ID for this request
        var requestId = Guid.NewGuid().ToString();

        // Add the request ID to the HttpContext items so it can be accessed throughout the request pipeline
        context.Items["RequestId"] = requestId;

        // Add request ID to response headers
        context.Response.Headers["X-Request-ID"] = requestId;

        var sw = Stopwatch.StartNew();

        // Log the start of the request
        logger.LogInformation("[{RequestId}] Starting {Path}", requestId, context.Request.GetEncodedPathAndQuery());

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
                "[{RequestId}] Completed {Path} | Status: {StatusCode} | Duration: {Duration}ms",
                requestId,
                context.Request.GetEncodedPathAndQuery(),
                context.Response.StatusCode,
                sw.ElapsedMilliseconds
            );
        }
    }
}
