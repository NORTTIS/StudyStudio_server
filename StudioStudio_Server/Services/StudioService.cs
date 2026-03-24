using Microsoft.AspNetCore.Http;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using StudioStudio_Server.Utils;
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
        private readonly IHttpContextAccessor _httpContextAccessor;

        public StudioService(
            IStudioRepository studioRepository,
            IGroupRepository groupRepository,
            IUserSubscriptionRepository userSubscriptionRepository,
            IStudioParticipantRepository studioParticipantRepository,
            IGroupParticipantRepository groupParticipantRepository,
            IHttpContextAccessor httpContextAccessor)
        {
            _studioRepository = studioRepository;
            _groupRepository = groupRepository;
            _userSubscriptionRepository = userSubscriptionRepository;
            _studioParticipantRepository = studioParticipantRepository;
            _groupParticipantRepository = groupParticipantRepository;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<StudioListResponse> GetUserStudiosAsync(Guid userId)
        {
            // Get subscription info
            var subscriptionPlan = await _userSubscriptionRepository.GetSubscriptionPlanByUserIdAsync(userId);
            var studioLimit = subscriptionPlan?.MaxStudios ?? 3;
            var studioCreated = await _studioRepository.CountStudioCreatedByUserAsync(userId);

            // Get studios where user is owner
            var ownedStudios = await _studioRepository.GetByOwnerIdAsync(userId);

            // Get studios where user is a participant (member)
            var participantRecords = await _studioParticipantRepository.GetStudiosByUserIdAsync(userId);
            var participantStudioIds = participantRecords
                .Where(pr => pr.StudioId != null)
                .Select(pr => pr.StudioId)
                .ToList();

            var memberStudios = new List<Studio>();
            if (participantStudioIds.Any())
            {
                // Get studio details for participant studios
                memberStudios = await _studioRepository.GetByIdsAsync(participantStudioIds);
                // Filter out studios where user is already the owner (to avoid duplicates)
                memberStudios = memberStudios.Where(s => s.OwnerId != userId).ToList();
            }

            // Combine owned and member studios (avoid duplicates)
            var allStudios = ownedStudios
                .Concat(memberStudios)
                .GroupBy(s => s.StudioId)
                .Select(g => g.First())
                .OrderByDescending(s => s.CreatedAt)
                .ToList();

            if (!allStudios.Any())
            {
                return new StudioListResponse
                {
                    Studios = new List<StudioResponse>(),
                    Subscription = new StudioListSubscriptionResponse
                    {
                        StudioLimit = studioLimit,
                        StudioCreated = studioCreated
                    }
                };
            }

            var studioResponses = allStudios.Select(studio => new StudioResponse
            {
                StudioId = studio.StudioId,
                StudioName = studio.StudioName,
                Description = studio.Description,
                OwnerId = studio.OwnerId,
                StudioRole = studio.OwnerId == userId ? StudioRole.Owner : StudioRole.Member,
                CreatedAt = studio.CreatedAt,
                UpdatedAt = studio.UpdatedAt,
                GroupCount = 0,
                StartDate = studio.StartDate,
                EndDate = studio.EndDate
            }).ToList();

            foreach (var response in studioResponses)
            {
                response.GroupCount = await _groupRepository.GetGroupCountByStudioIdAsync(response.StudioId);
            }

            return new StudioListResponse
            {
                Studios = studioResponses,
                Subscription = new StudioListSubscriptionResponse
                {
                    StudioLimit = studioLimit,
                    StudioCreated = studioCreated
                }
            };
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

            // Validate: EndDate must be >= StartDate if both are provided
            if (studio.StartDate.HasValue && studio.EndDate.HasValue && studio.EndDate < studio.StartDate)
            {
                throw new AppException(ErrorCodes.StudioInvalidDateRange, StatusCodes.Status400BadRequest);
            }

            var now = DateTime.UtcNow;
            var createStudio = new Studio
            {
                StudioId = Guid.NewGuid(),
                OwnerId = ownerId,
                StudioName = studio.StudioName,
                Description = studio.Description,
                CreatedAt = now,
                UpdatedAt = now,
                // Normalize to UTC for PostgreSQL timestamp with time zone compatibility
                StartDate = studio.StartDate.HasValue ? DateTime.SpecifyKind(studio.StartDate.Value, DateTimeKind.Utc) : null,
                EndDate = studio.EndDate.HasValue ? DateTime.SpecifyKind(studio.EndDate.Value, DateTimeKind.Utc) : null
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
                GroupCount = 0,
                StartDate = createStudio.StartDate,
                EndDate = createStudio.EndDate
            };
        }

        public async Task<StudioResponse> GetStudioDetailAsync(Guid userId, Guid studioId)
        {
            var studio = await _studioRepository.GetByIdAsync(studioId);
            if (studio == null)
            {
                throw new AppException(ErrorCodes.StudioNotFound, StatusCodes.Status404NotFound);
            }

            // Check if user is owner or member of studio
            var userStudioParticipant = await _studioParticipantRepository.GetByStudioAndUserAsync(studioId, userId);
            if (studio.OwnerId != userId && userStudioParticipant == null)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            int groupCount;
            if (studio.OwnerId == userId)
            {
                // Owner can see all groups in studio
                groupCount = await _groupRepository.GetGroupCountByStudioIdAsync(studioId);
            }
            else
            {
                // Member can only see groups they participate in
                var studioGroups = await _groupRepository.GetStudioGroupsAsync(studioId);
                var groupIds = studioGroups.Select(g => g.GroupId).ToList();

                if (!groupIds.Any())
                {
                    groupCount = 0;
                }
                else
                {
                    var userGroupParticipants = await _groupParticipantRepository.GetByGroupIdsAsync(groupIds);
                    groupCount = userGroupParticipants.Count(gp => gp.UserId == userId);
                }
            }

            return new StudioResponse
            {
                StudioId = studio.StudioId,
                StudioName = studio.StudioName,
                Description = studio.Description,
                OwnerId = studio.OwnerId,
                StudioRole = studio.OwnerId == userId ? StudioRole.Owner : StudioRole.Member,
                CreatedAt = studio.CreatedAt,
                UpdatedAt = studio.UpdatedAt,
                GroupCount = groupCount,
                StartDate = studio.StartDate,
                EndDate = studio.EndDate,
                AvatarUrl = studio.AvatarUrl,
                ColorHex = studio.ColorHex
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

            // Validate: EndDate must be >= StartDate if both are provided
            if (studio.StartDate.HasValue && studio.EndDate.HasValue && studio.EndDate < studio.StartDate)
            {
                throw new AppException(ErrorCodes.StudioInvalidDateRange, StatusCodes.Status400BadRequest);
            }

            updateStudio.StudioName = studio.StudioName;
            updateStudio.Description = studio.Description;
            // Normalize to UTC for PostgreSQL timestamp with time zone compatibility
            updateStudio.StartDate = studio.StartDate.HasValue ? DateTime.SpecifyKind(studio.StartDate.Value, DateTimeKind.Utc) : null;
            updateStudio.EndDate = studio.EndDate.HasValue ? DateTime.SpecifyKind(studio.EndDate.Value, DateTimeKind.Utc) : null;

            // 🔹 ADDED: Validate and update personalization fields
            if (!string.IsNullOrEmpty(studio.ColorHex))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(studio.ColorHex, @"^#[0-9A-Fa-f]{6}$"))
                {
                    throw new AppException(ErrorCodes.ValidationInvalidColor, StatusCodes.Status400BadRequest);
                }
                updateStudio.ColorHex = studio.ColorHex;
            }
            else if (studio.ColorHex == null)
            {
                updateStudio.ColorHex = null;
            }

            updateStudio.AvatarUrl = studio.AvatarUrl;
            updateStudio.UpdatedAt = DateTime.UtcNow;

            await _studioRepository.UpdateStudioAsync(updateStudio);

            return new UpdateStudioResponse
            {
                StudioName = updateStudio.StudioName,
                Description = updateStudio.Description,
                UpdatedAt = updateStudio.UpdatedAt,
                StartDate = updateStudio.StartDate,
                EndDate = updateStudio.EndDate,
                AvatarUrl = updateStudio.AvatarUrl,
                ColorHex = updateStudio.ColorHex
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
                    AvatarUrl = AvatarUrlHelper.BuildAbsoluteAvatarUrl(participant.User.AvatarUrl, _httpContextAccessor.HttpContext),
                    StudioRole = participant.Role,
                    GroupInfo = groupInfoList
                });
            }

            return result;
        }
    }
}
