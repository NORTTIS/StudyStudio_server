using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller qu?n l? Favourites (groups yêu thích)
    /// Route: /api/favourite
    /// </summary>
    [Route("api/favourite")]
    [ApiController]
    [Authorize]
    public class FavouriteController : ControllerBase
    {
        private readonly IFavouriteService _favouriteService;
        private readonly IMessageService _messageService;

        public FavouriteController(
            IFavouriteService favouriteService,
            IMessageService messageService)
        {
            _favouriteService = favouriteService;
            _messageService = messageService;
        }

        /// <summary>
        /// Xác th?c và l?y userId t? JWT token
        /// Validate: User không ðý?c là admin
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
        /// Thêm group vào danh sách yêu thích
        /// Validate:
        /// - Group ph?i t?n t?i
        /// - User ph?i là member c?a group
        /// - Group chýa có trong favourites
        /// </summary>
        [HttpPost("add")]
        public async Task<ActionResult<ApiResponse<FavouriteResponse>>> AddFavourite(
            [FromBody] AddFavouriteRequest request)
        {
            var userId = ValidateAndGetUserId();
            var result = await _favouriteService.AddFavouriteAsync(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessAddFavourite);

            return Ok(ApiResponse<FavouriteResponse>.Success(
                ErrorCodes.SuccessAddFavourite,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] DELETE /api/favourite/remove
        /// Xóa group kh?i danh sách yêu thích
        /// Validate: Favourite ph?i t?n t?i
        /// </summary>
        [HttpDelete("remove")]
        public async Task<ActionResult<ApiResponse<object>>> RemoveFavourite(
            [FromBody] RemoveFavouriteRequest request)
        {
            var userId = ValidateAndGetUserId();
            await _favouriteService.RemoveFavouriteAsync(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessRemoveFavourite);

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
