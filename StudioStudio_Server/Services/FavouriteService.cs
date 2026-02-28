using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service x? l? business logic cho Favourites
    /// </summary>
    public class FavouriteService : IFavouriteService
    {
        private readonly IFavouriteRepository _favouriteRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IGroupParticipantRepository _groupParticipantRepository;
        private readonly ILogger<FavouriteService> _logger;

        public FavouriteService(
            IFavouriteRepository favouriteRepository,
            IGroupRepository groupRepository,
            IGroupParticipantRepository groupParticipantRepository,
            ILogger<FavouriteService> logger)
        {
            _favouriteRepository = favouriteRepository;
            _groupRepository = groupRepository;
            _groupParticipantRepository = groupParticipantRepository;
            _logger = logger;
        }

        /// <summary>
        /// Thêm group vào danh sách yêu thích
        /// Validate:
        /// - Group ph?i t?n t?i
        /// - User ph?i là member c?a group
        /// - Chýa có trong favourites
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
        /// Xóa group kh?i danh sách yêu thích
        /// Validate: Favourite ph?i t?n t?i
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
        /// Validate group t?n t?i
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
        /// Validate user là member c?a group
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
        /// Validate group chýa có trong favourites
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
        /// Validate favourite t?n t?i và thu?c v? user
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
