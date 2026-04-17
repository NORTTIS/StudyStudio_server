using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service handling business logic for Favourites (user's favourite groups)
    /// Allows users to mark groups as favourites for quick access
    /// Only group members can add group to favourites
    /// </summary>
    public class FavouriteService(
        IFavouriteRepository favouriteRepository,
        IGroupRepository groupRepository,
        IGroupParticipantRepository groupParticipantRepository,
        ILogger<FavouriteService> logger) : IFavouriteService
    {
        private readonly IFavouriteRepository _favouriteRepository = favouriteRepository;
        private readonly IGroupRepository _groupRepository = groupRepository;
        private readonly IGroupParticipantRepository _groupParticipantRepository = groupParticipantRepository;
        private readonly ILogger<FavouriteService> _logger = logger;

        /// <summary>
        /// Add group to user's favourites
        /// Validate:
        /// - Group must exist
        /// - User must be a member of the group
        /// - Group not already in favourites
        /// </summary>
        public async Task<FavouriteResponse> AddFavouriteAsync(
            Guid userId,
            AddFavouriteRequest request)
        {
            var group = await ValidateGroupExistsAsync(request.GroupId);
            await ValidateUserIsMemberAsync(request.GroupId, userId);
            await ValidateFavouriteNotExistsAsync(userId, request.GroupId);

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

            return new FavouriteResponse
            {
                FavouriteId = favourite.FavouriteId,
                GroupId = group.GroupId,
                GroupName = group.GroupName,
                CreatedAt = favourite.CreatedAt
            };
        }

        /// <summary>
        /// Remove group from user's favourites
        /// Validate: Favourite record must exist
        /// Action: Hard delete from database
        /// </summary>
        public async Task RemoveFavouriteAsync(
            Guid userId,
            RemoveFavouriteRequest request)
        {
            var favourite = await ValidateFavouriteExistsAsync(userId, request.GroupId);

            await _favouriteRepository.RemoveAsync(favourite);

            _logger.LogInformation(
                "User {UserId} removed group {GroupId} from favourites",
                userId, request.GroupId);
        }

        /// <summary>
        /// Validate group exists
        /// Throws AppException if group not found
        /// </summary>
        private async Task<Group> ValidateGroupExistsAsync(Guid groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);

            if (group == null)
            {
                throw new AppException(
                    ErrorCodes.GroupNotFound,
                    StatusCodes.Status404NotFound);
            }

            return group;
        }

        /// <summary>
        /// Validate user is a member of the group
        /// Throws AppException if user is not a member
        /// </summary>
        private async Task ValidateUserIsMemberAsync(Guid groupId, Guid userId)
        {
            var isMember = await _groupParticipantRepository
                .IsUserInGroupAsync(groupId, userId);

            if (!isMember)
            {
                throw new AppException(
                    ErrorCodes.FavouriteNotMember,
                    StatusCodes.Status403Forbidden);
            }
        }

        /// <summary>
        /// Validate group is not already in user's favourites
        /// Throws AppException if favourite already exists
        /// </summary>
        private async Task ValidateFavouriteNotExistsAsync(Guid userId, Guid groupId)
        {
            var exists = await _favouriteRepository.ExistsAsync(userId, groupId);

            if (exists)
            {
                throw new AppException(
                    ErrorCodes.FavouriteAlreadyExists,
                    StatusCodes.Status400BadRequest);
            }
        }

        /// <summary>
        /// Validate favourite exists and belongs to user
        /// Throws AppException if favourite not found
        /// </summary>
        private async Task<Favourite> ValidateFavouriteExistsAsync(Guid userId, Guid groupId)
        {
            var favourite = await _favouriteRepository
                .GetByUserAndGroupIdAsync(userId, groupId);

            if (favourite == null)
            {
                throw new AppException(
                    ErrorCodes.FavouriteNotFound,
                    StatusCodes.Status404NotFound);
            }

            return favourite;
        }
    }
}
