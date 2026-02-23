using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    [Route("api/favourite")]
    [ApiController]
    public class FavouriteController : ControllerBase
    {
        private readonly ILogger<FavouriteController> _logger;
        private readonly IMessageService _messageService;
        private readonly IFavouriteRepository _favouriteRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IGroupParticipantRepository _groupParticipantRepository;

        public FavouriteController(
            ILogger<FavouriteController> logger,
            IMessageService messageService,
            IFavouriteRepository favouriteRepository,
            IGroupRepository groupRepository,
            IGroupParticipantRepository groupParticipantRepository)
        {
            _logger = logger;
            _messageService = messageService;
            _favouriteRepository = favouriteRepository;
            _groupRepository = groupRepository;
            _groupParticipantRepository = groupParticipantRepository;
        }

        [HttpPost("add")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<FavouriteResponse>>> AddFavourite(
            [FromBody] AddFavouriteRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null && bool.TryParse(isAdminClaim, out var adminResult) && adminResult;
            if (isAdmin)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            // Check if group exists
            var group = await _groupRepository.GetByIdAsync(request.GroupId);
            if (group == null)
            {
                throw new AppException(ErrorCodes.GroupNotFound, StatusCodes.Status404NotFound);
            }

            // Check if user is a member of the group
            var isMember = await _groupParticipantRepository.IsUserInGroupAsync(request.GroupId, userId);
            if (!isMember)
            {
                throw new AppException(ErrorCodes.FavouriteNotMember, StatusCodes.Status403Forbidden);
            }

            // Check if already in favourites
            var alreadyExists = await _favouriteRepository.ExistsAsync(userId, request.GroupId);
            if (alreadyExists)
            {
                throw new AppException(ErrorCodes.FavouriteAlreadyExists, StatusCodes.Status400BadRequest);
            }

            // Add to favourites
            var favourite = new Favourite
            {
                FavouriteId = Guid.NewGuid(),
                UserId = userId,
                GroupId = request.GroupId,
                CreatedAt = DateTime.UtcNow
            };

            await _favouriteRepository.AddAsync(favourite);

            _logger.LogInformation(
                "User {UserId} added group {GroupId} to favourites",
                userId, request.GroupId);

            var response = new FavouriteResponse
            {
                FavouriteId = favourite.FavouriteId,
                GroupId = group.GroupId,
                GroupName = group.GroupName,
                CreatedAt = favourite.CreatedAt
            };

            var message = _messageService.GetMessage(ErrorCodes.SuccessAddFavourite);
            return Ok(ApiResponse<FavouriteResponse>.Success(ErrorCodes.SuccessAddFavourite, message, response));
        }

        [HttpDelete("remove")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<object>>> RemoveFavourite(
            [FromBody] RemoveFavouriteRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null && bool.TryParse(isAdminClaim, out var adminResult) && adminResult;
            if (isAdmin)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            // Check if favourite exists
            var favourite = await _favouriteRepository.GetByUserAndGroupIdAsync(userId, request.GroupId);
            if (favourite == null)
            {
                throw new AppException(ErrorCodes.FavouriteNotFound, StatusCodes.Status404NotFound);
            }

            // Remove from favourites
            await _favouriteRepository.RemoveAsync(favourite);

            _logger.LogInformation(
                "User {UserId} removed group {GroupId} from favourites",
                userId, request.GroupId);

            var message = _messageService.GetMessage(ErrorCodes.SuccessRemoveFavourite);
            return Ok(ApiResponse<object>.Success(ErrorCodes.SuccessRemoveFavourite, message, new
            {
                groupId = request.GroupId,
                removedAt = DateTime.UtcNow
            }));
        }
    }
}
