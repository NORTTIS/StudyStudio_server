using StudioStudio_Server.Interceptors;

namespace StudioStudio_Server.Middlewares;

/// <summary>
/// Middleware that tracks SQL query count per HTTP request using QueryCounterInterceptor.
/// Logs: method, path, query count, duration.
/// Targets specific slow paths for Phase 1 baseline measurement.
/// </summary>
public class QueryCounterMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<QueryCounterMiddleware> _logger;

    // Baseline paths to measure
    private static readonly string[] TrackedPaths = new[]
    {
        "/api/group/",    // GetGroupDetailAsync
        "/api/Task/"      // Task update/reorder
    };

    public QueryCounterMiddleware(RequestDelegate next, ILogger<QueryCounterMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        var shouldTrack = TrackedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        if (shouldTrack)
        {
            QueryCounterInterceptor.BeginRequest();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                await _next(context);
            }
            finally
            {
                sw.Stop();
                var info = QueryCounterInterceptor.EndRequest();
                if (info != null)
                {
                    _logger.LogInformation(
                        "[QUERY-METRIC] {Method} {Path} => {StatusCode} | Queries: {QueryCount} | Duration: {DurationMs}ms | Summary: {Summary}",
                        context.Request.Method,
                        path,
                        context.Response.StatusCode,
                        info.Count,
                        sw.ElapsedMilliseconds,
                        info.GetSummary());
                }
            }
        }
        else
        {
            await _next(context);
        }
    }
}

/// <summary>
/// Extension method for easy middleware registration.
/// </summary>
public static class QueryCounterMiddlewareExtensions
{
    public static IApplicationBuilder UseQueryCounter(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<QueryCounterMiddleware>();
    }
}
