using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Services
{
    public class StudioService : IStudioService
    {
        private readonly IStudioRepository _studioRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IUserSubscriptionRepository _userSubscriptionRepository;

        public StudioService(
            IStudioRepository studioRepository,
            IGroupRepository groupRepository,
            IUserSubscriptionRepository userSubscriptionRepository)
        {
            _studioRepository = studioRepository;
            _groupRepository = groupRepository;
            _userSubscriptionRepository = userSubscriptionRepository;
        }

        public async Task<List<StudioResponse>> GetUserStudiosAsync(Guid userId)
        {
            var studios = await _studioRepository.GetByOwnerIdAsync(userId);

            if (!studios.Any())
            {
                return new List<StudioResponse>();
            }

            var studioResponses = studios.Select(studio => new StudioResponse
            {
                StudioId = studio.StudioId,
                StudioName = studio.StudioName,
                Description = studio.Description,
                OwnerId = studio.OwnerId,
                CreatedAt = studio.CreatedAt,
                UpdatedAt = studio.UpdatedAt,
                GroupCount = 0
            }).ToList();

            foreach (var response in studioResponses)
            {
                response.GroupCount = await _groupRepository.GetGroupCountByStudioIdAsync(response.StudioId);
            }

            return studioResponses;
        }

        public async Task<StudioResponse> CreateStudioAsync(Guid ownerId, CreateStudioRequest studio)
        {
            var subscriptionPlan = await _userSubscriptionRepository.GetSubscriptionPlanByUserIdAsync(ownerId);
            var studioLimit = subscriptionPlan?.MaxStudios ?? 3;

            var currentStudioCount = await _studioRepository.CountStudioCreatedByUserAsync(ownerId);
            if (currentStudioCount >= studioLimit)
            {
                throw new AppException(ErrorCodes.StudioLimitReached, StatusCodes.Status403Forbidden);
            }

            var now = DateTime.UtcNow;
            var createStudio = new Studio
            {
                StudioId = Guid.NewGuid(),
                OwnerId = ownerId,
                StudioName = studio.StudioName,
                Description = studio.Description,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _studioRepository.CreateStudioAsync(createStudio);

            return new StudioResponse
            {
                StudioId = createStudio.StudioId,
                StudioName = createStudio.StudioName,
                OwnerId = createStudio.OwnerId,
                Description = createStudio.Description,
                CreatedAt = now,
                UpdatedAt = now,
                GroupCount = 0
            };
        }

        public async Task<StudioResponse> GetStudioDetailAsync(Guid userId, Guid studioId)
        {
            var studio = await _studioRepository.GetByIdAsync(studioId);
            if (studio == null)
            {
                throw new AppException(ErrorCodes.StudioNotFound, StatusCodes.Status404NotFound);
            }

            if (studio.OwnerId != userId)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            var groupCount = await _groupRepository.GetGroupCountByStudioIdAsync(studioId);

            return new StudioResponse
            {
                StudioId = studio.StudioId,
                StudioName = studio.StudioName,
                Description = studio.Description,
                OwnerId = studio.OwnerId,
                CreatedAt = studio.CreatedAt,
                UpdatedAt = studio.UpdatedAt,
                GroupCount = groupCount
            };
        }

        public async Task DeleteStudioAsync(Guid userId, Guid studioId)
        {
            var deleteStudio = await _studioRepository.GetByIdAsync(studioId);
            if (deleteStudio == null)
            {
                throw new AppException(ErrorCodes.StudioNotFound, StatusCodes.Status404NotFound);
            }

            if (deleteStudio.OwnerId != userId)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            var groupInStud = await _groupRepository.GetStudioGroupsAsync(studioId);
            if (groupInStud.Any())
            {
                foreach (var group in groupInStud)
                {
                    group.IsActive = false;
                }
                await _groupRepository.SaveChangesAsync();
            }
            await _studioRepository.DeleteStudioAsync(deleteStudio);
        }

        public async Task<UpdateStudioResponse> UpdateStudioAsync(Guid userId, UpdateStudioRequest studio)
        {
            var updateStudio = await _studioRepository.GetByIdAsync(studio.Id);
            if (updateStudio == null)
            {
                throw new AppException(ErrorCodes.StudioNotFound, StatusCodes.Status404NotFound);
            }

            if (updateStudio.OwnerId != userId)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            updateStudio.StudioName = studio.StudioName;
            updateStudio.Description = studio.Description;
            updateStudio.UpdatedAt = DateTime.UtcNow;

            await _studioRepository.UpdateStudioAsync(updateStudio);

            return new UpdateStudioResponse
            {
                StudioName = updateStudio.StudioName,
                Description = updateStudio.Description,
                UpdatedAt = updateStudio.UpdatedAt
            };
        }
    }
}
