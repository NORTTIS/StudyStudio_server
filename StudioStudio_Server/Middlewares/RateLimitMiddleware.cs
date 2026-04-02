using StackExchange.Redis;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Localization;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Utils;

namespace StudioStudio_Server.Middlewares
{
    /// <summary>
    /// Simple rate limiting middleware using Redis
    /// - Limit API calls per user per minute
    /// - Prevent abuse and DDoS attacks
    /// </summary>
    public class RateLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimitMiddleware> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly IConnectionMultiplexer _redis;

        // Configuration: Max requests per minute per user
        private const int MAX_REQUESTS_PER_MINUTE = 300;
        private const int RATE_LIMIT_WINDOW_SECONDS = 60;

        public RateLimitMiddleware(
            RequestDelegate next,
            ILogger<RateLimitMiddleware> logger,
            IWebHostEnvironment env,
            IConnectionMultiplexer redis)
        {
            _next = next;
            _logger = logger;
            _env = env;
            _redis = redis;
        }

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
                await _next(context);
                return;
            }

            // Check if user is authenticated
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                try
                {
                    // Skip rate limiting for admin users
                    if (JwtHelper.IsAdmin(context.User))
                    {
                        await _next(context);
                        return;
                    }

                    var userId = JwtHelper.TryGetUserId(context.User);

                    if (userId.HasValue)
                    {
                        // Check rate limit
                        var db = _redis.GetDatabase();
                        var key = $"ratelimit:user:{userId.Value}";

                        // Get current request count
                        var currentCount = await db.StringGetAsync(key);
                        
                        if (currentCount.HasValue && int.TryParse(currentCount, out int count))
                        {
                            if (count >= MAX_REQUESTS_PER_MINUTE)
                            {
                                _logger.LogWarning(
                                    "Rate limit exceeded for user {UserId}. Count: {Count}, Path: {Path}", 
                                    userId.Value, 
                                    count,
                                    path);
                                
                                await HandleRateLimitExceeded(context);
                                return;
                            }

                            // Increment counter
                            await db.StringIncrementAsync(key);
                        }
                        else
                        {
                            // First request in this window - set counter and expiry
                            await db.StringSetAsync(key, 1, TimeSpan.FromSeconds(RATE_LIMIT_WINDOW_SECONDS));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in rate limit middleware");
                    // Don't block request if Redis fails
                }
            }

            await _next(context);
        }

        private async Task HandleRateLimitExceeded(HttpContext context)
        {
            var culture = HttpContextHelper.GetCultureFromHeader(context);
            var localizer = new JsonStringLocalizer(_env, culture);

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
