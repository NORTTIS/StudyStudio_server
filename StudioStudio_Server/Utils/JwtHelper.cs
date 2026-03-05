using StudioStudio_Server.Exceptions;
using System.Security.Claims;

namespace StudioStudio_Server.Utils
{
    /// <summary>
    /// Utility class for JWT token and claims validation
    /// </summary>
    public static class JwtHelper
    {
        /// <summary>
        /// Extract and validate userId from ClaimsPrincipal
        /// Throws AppException if userId is invalid or missing
        /// </summary>
        /// <param name="user">ClaimsPrincipal from controller or hub context</param>
        /// <returns>Validated userId</returns>
        /// <exception cref="AppException">If userId is missing or invalid format</exception>
        public static Guid GetUserId(ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(
                    ErrorCodes.AuthInvalidCredential,
                    StatusCodes.Status401Unauthorized);
            }

            return userId;
        }

        /// <summary>
        /// Check if user is admin from claims
        /// </summary>
        /// <param name="user">ClaimsPrincipal from controller or hub context</param>
        /// <returns>True if user is admin, false otherwise</returns>
        public static bool IsAdmin(ClaimsPrincipal user)
        {
            var isAdminClaim = user.FindFirst("IsAdmin")?.Value;
            return isAdminClaim != null &&
                   bool.TryParse(isAdminClaim, out var adminResult) &&
                   adminResult;
        }

        /// <summary>
        /// Extract and validate userId from ClaimsPrincipal
        /// Also validate that user is NOT admin (for user APIs)
        /// Throws AppException if:
        /// - userId is invalid or missing
        /// - user is admin (admin cannot use user APIs)
        /// </summary>
        /// <param name="user">ClaimsPrincipal from controller context</param>
        /// <returns>Validated userId</returns>
        /// <exception cref="AppException">If userId invalid or user is admin</exception>
        public static Guid ValidateAndGetUserId(ClaimsPrincipal user)
        {
            var userId = GetUserId(user);

            if (IsAdmin(user))
            {
                throw new AppException(
                    ErrorCodes.AuthForbidden,
                    StatusCodes.Status403Forbidden);
            }

            return userId;
        }

        /// <summary>
        /// Validate user is admin
        /// Throws AppException if:
        /// - userId is invalid or missing
        /// - user is NOT admin
        /// </summary>
        /// <param name="user">ClaimsPrincipal from controller context</param>
        /// <returns>Validated userId (admin user)</returns>
        /// <exception cref="AppException">If userId invalid or user is not admin</exception>
        public static Guid ValidateAdminUser(ClaimsPrincipal user)
        {
            var userId = GetUserId(user);

            if (!IsAdmin(user))
            {
                throw new AppException(
                    ErrorCodes.AuthForbidden,
                    StatusCodes.Status403Forbidden);
            }

            return userId;
        }

        /// <summary>
        /// Try to extract userId from ClaimsPrincipal without throwing exception
        /// Returns null if userId is missing or invalid
        /// </summary>
        /// <param name="user">ClaimsPrincipal from controller or hub context</param>
        /// <returns>UserId if valid, null otherwise</returns>
        public static Guid? TryGetUserId(ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                return null;
            }

            return userId;
        }
    }
}
