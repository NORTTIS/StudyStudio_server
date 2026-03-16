using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
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
        private readonly IStudioParticipantRepository _studioParticipantRepository;
        private readonly IGroupParticipantRepository _groupParticipantRepository;

        public StudioService(
            IStudioRepository studioRepository,
            IGroupRepository groupRepository,
            IUserSubscriptionRepository userSubscriptionRepository,
            IStudioParticipantRepository studioParticipantRepository,
            IGroupParticipantRepository groupParticipantRepository)
        {
            _studioRepository = studioRepository;
            _groupRepository = groupRepository;
            _userSubscriptionRepository = userSubscriptionRepository;
            _studioParticipantRepository = studioParticipantRepository;
            _groupParticipantRepository = groupParticipantRepository;
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

            // Auto-set creator as Owner in StudioParticipant
            var studioParticipant = new StudioParticipant
            {
                ParticipantId = Guid.NewGuid(),
                StudioId = createStudio.StudioId,
                UserId = ownerId,
                Role = StudioRole.Owner,
                CreatedAt = now
            };
            await _studioParticipantRepository.AddAsync(studioParticipant);

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

        public async Task<List<StudioMemberResponse>> GetStudioMembersAsync(Guid userId, Guid studioId)
        {
            // Validate: User must be member or owner of studio
            var userStudioParticipant = await _studioParticipantRepository.GetByStudioAndUserAsync(studioId, userId);
            if (userStudioParticipant == null)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            // Get all participants in the studio
            var studioParticipants = await _studioParticipantRepository.GetParticipantsByStudioIdAsync(studioId);

            // Get all group IDs in this studio
            var studioGroups = await _groupRepository.GetStudioGroupsAsync(studioId);
            var groupIds = studioGroups.Select(g => g.GroupId).ToList();

            // Get all group participants for users in this studio (only for groups in this studio)
            var userIds = studioParticipants.Select(sp => sp.UserId).ToList();
            var allGroupParticipants = new List<GroupParticipant>();

            if (groupIds.Any())
            {
                allGroupParticipants = await _groupParticipantRepository.GetByGroupIdsAsync(groupIds);
            }

            // Build response
            var result = new List<StudioMemberResponse>();

            foreach (var participant in studioParticipants)
            {
                var userGroups = allGroupParticipants
                    .Where(gp => gp.UserId == participant.UserId)
                    .ToList();

                var groupInfoList = new List<GroupInfoItem>();
                foreach (var ug in userGroups)
                {
                    var group = studioGroups.FirstOrDefault(g => g.GroupId == ug.GroupId);
                    if (group != null)
                    {
                        groupInfoList.Add(new GroupInfoItem
                        {
                            GroupId = ug.GroupId,
                            GroupName = group.GroupName,
                            GroupRole = ug.Role
                        });
                    }
                }

                result.Add(new StudioMemberResponse
                {
                    UserId = participant.UserId,
                    UserName = $"{participant.User.FirstName} {participant.User.LastName}",
                    Email = participant.User.Email,
                    StudioRole = participant.Role,
                    GroupInfo = groupInfoList
                });
            }

            return result;
        }
    }
}
