using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller for managing Favourites (favourite groups)
    /// Route: /api/favourite
    /// </summary>
    [Route("api/favourite")]
    [ApiController]
    [Authorize]
    public class FavouriteController(
        IFavouriteService favouriteService,
        IMessageService messageService) : ControllerBase
    {
        /// <summary>
        /// Authenticate and get userId from JWT token
        /// Validate: User must not be admin
        /// </summary>
        private Guid ValidateAndGetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(
                    ErrorCodes.AuthInvalidCredential,
                    StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null &&
                          bool.TryParse(isAdminClaim, out var adminResult) &&
                          adminResult;

            if (isAdmin)
            {
                throw new AppException(
                    ErrorCodes.AuthForbidden,
                    StatusCodes.Status403Forbidden);
            }

            return userId;
        }

        /// <summary>
        /// [AUTHORIZED] POST /api/favourite/add
        /// Add group to favourites list
        /// Validate:
        /// - Group must exist
        /// - User must be member of group
        /// - Group not already in favourites
        /// </summary>
        [HttpPost("add")]
        public async Task<ActionResult<ApiResponse<FavouriteResponse>>> AddFavourite(
            [FromBody] AddFavouriteRequest request)
        {
            var userId = ValidateAndGetUserId();
            var result = await favouriteService.AddFavouriteAsync(userId, request);
            var message = messageService.GetMessage(ErrorCodes.SuccessAddFavourite);

            return Ok(ApiResponse<FavouriteResponse>.Success(
                ErrorCodes.SuccessAddFavourite,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] DELETE /api/favourite/remove
        /// Remove group from favourites list
        /// Validate: Favourite must exist
        /// </summary>
        [HttpDelete("remove")]
        public async Task<ActionResult<ApiResponse<object>>> RemoveFavourite(
            [FromBody] RemoveFavouriteRequest request)
        {
            var userId = ValidateAndGetUserId();
            await favouriteService.RemoveFavouriteAsync(userId, request);
            var message = messageService.GetMessage(ErrorCodes.SuccessRemoveFavourite);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessRemoveFavourite,
                message,
                new
                {
                    groupId = request.GroupId,
                    removedAt = DateTime.UtcNow
                }));
        }
    }
}
