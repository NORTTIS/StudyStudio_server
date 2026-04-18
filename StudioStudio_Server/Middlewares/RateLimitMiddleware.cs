using StackExchange.Redis;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Resources.Localization;
using StudioStudio_Server.Utils;

namespace StudioStudio_Server.Middlewares
{
    /// <summary>
    /// Simple rate limiting middleware using Redis
    /// - Limit API calls per user per minute
    /// - Prevent abuse and DDoS attacks
    /// </summary>
    public class RateLimitMiddleware(
        RequestDelegate next,
        ILogger<RateLimitMiddleware> logger,
        IWebHostEnvironment env,
        IConnectionMultiplexer redis)
    {
        // Configuration: Max requests per window per user
        private const int MAX_REQUESTS_PER_WINDOW = 500;
        private const int RATE_LIMIT_WINDOW_SECONDS = 30;

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip rate limiting for:
            // 1. Auth endpoints (login, register have their own rate limiting)
            // 2. Public endpoints
            // 3. Swagger
            // 4. Admin users
            var path = context.Request.Path.Value?.ToLower() ?? "";

            if (path.StartsWith("/api/auth") ||
                path.StartsWith("/swagger") ||
                path.StartsWith("/hubs") ||
                !path.StartsWith("/api"))
            {
                await next(context);
                return;
            }

            // Check if user is authenticated
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                // Admin traffic is bypassed entirely and should not be wrapped by rate-limit error logging.
                if (JwtHelper.IsAdmin(context.User))
                {
                    await next(context);
                    return;
                }

                try
                {
                    var userId = JwtHelper.TryGetUserId(context.User);

                    if (userId.HasValue)
                    {
                        // Fixed time-based window: counter resets every RATE_LIMIT_WINDOW_SECONDS
                        // Key includes window number so expired windows are automatically skipped
                        var db = redis.GetDatabase();
                        var windowStart = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / RATE_LIMIT_WINDOW_SECONDS;
                        var key = $"ratelimit:user:{userId.Value}:{windowStart}";

                        // Atomic increment, then check
                        var newCount = await db.StringIncrementAsync(key);
                        await db.KeyExpireAsync(key, TimeSpan.FromSeconds(RATE_LIMIT_WINDOW_SECONDS * 2));

                        if (newCount > MAX_REQUESTS_PER_WINDOW)
                        {
                            logger.LogWarning(
                                "Rate limit exceeded for user {UserId}. Count: {Count}, Path: {Path}",
                                userId.Value,
                                newCount,
                                path);

                            await HandleRateLimitExceeded(context);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error in rate limit middleware");
                    // Don't block request if Redis fails
                }
            }

            await next(context);
        }

        private async Task HandleRateLimitExceeded(HttpContext context)
        {
            var culture = HttpContextHelper.GetCultureFromHeader(context);
            var localizer = new JsonStringLocalizer(env, culture);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";

            // Add Retry-After header
            context.Response.Headers["Retry-After"] = RATE_LIMIT_WINDOW_SECONDS.ToString();

            var response = ApiResponse<object>.Error(
                ErrorCodes.ApiRateLimitExceeded,
                localizer.Get(ErrorCodes.ApiRateLimitExceeded)
            );

            await context.Response.WriteAsJsonAsync(response);
        }
    }
}
