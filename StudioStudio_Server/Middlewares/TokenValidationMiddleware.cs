using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Resources.Localization;
using StudioStudio_Server.Utils;

namespace StudioStudio_Server.Middlewares
{
    /// <summary>
    /// Middleware to validate JWT access token and user status
    /// - Check if user still exists and is active
    /// - Check if user has not been deleted
    /// - Apply rate limiting for API calls
    /// </summary>
    public class TokenValidationMiddleware(
        RequestDelegate next,
        ILogger<TokenValidationMiddleware> logger,
        IWebHostEnvironment env)
    {
        public async Task InvokeAsync(HttpContext context, StudioDbContext dbContext)
        {
            // Skip validation for:
            // 1. Auth endpoints (login, register, etc.)
            // 2. Public endpoints
            // 3. Swagger
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
                try
                {
                    // Extract userId from JWT claims using helper
                    var userId = JwtHelper.TryGetUserId(context.User);
                    
                    if (userId.HasValue)
                    {
                        // Check if user still exists and is active
                        var user = await dbContext.Users
                            .AsNoTracking()
                            .FirstOrDefaultAsync(u => u.UserId == userId.Value);

                        if (user == null)
                        {
                            logger.LogWarning("User {UserId} not found in database but has valid token", userId.Value);
                            await HandleUnauthorized(context, ErrorCodes.UserNotFound);
                            return;
                        }

                        // Check if user account has been deleted
                        if (user.Status == UserStatus.Deleted)
                        {
                            logger.LogWarning("User {UserId} account is deleted but attempting to access API", userId.Value);
                            await HandleUnauthorized(context, ErrorCodes.UserAccountAlreadyDeleted);
                            return;
                        }

                        // Check if user account is inactive (disabled by admin)
                        if (user.Status == UserStatus.Inactive)
                        {
                            logger.LogWarning("User {UserId} account is inactive but attempting to access API", userId.Value);
                            await HandleUnauthorized(context, ErrorCodes.AuthAccountInactive);
                            return;
                        }

                        // Optional: Add user info to HttpContext.Items for later use
                        context.Items["User"] = user;
                        context.Items["UserId"] = userId.Value;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error validating token in middleware");
                    // Continue to next middleware - let controller handle if needed
                }
            }

            await next(context);
        }

        private async Task HandleUnauthorized(HttpContext context, string errorCode)
        {
            var culture = HttpContextHelper.GetCultureFromHeader(context);
            var localizer = new JsonStringLocalizer(env, culture);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";

            var response = ApiResponse<object>.Error(
                errorCode,
                localizer.Get(errorCode)
            );

            await context.Response.WriteAsJsonAsync(response);
        }
    }
}
