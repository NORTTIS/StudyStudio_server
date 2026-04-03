using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Configurations;
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
        private readonly IUserRepository _userRepository;
        private readonly IEmailService _emailService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<StudioService> _logger;
        private readonly IConfiguration _configuration;

        public StudioService(
            IStudioRepository studioRepository,
            IGroupRepository groupRepository,
            IUserSubscriptionRepository userSubscriptionRepository,
            IStudioParticipantRepository studioParticipantRepository,
            IGroupParticipantRepository groupParticipantRepository,
            IUserRepository userRepository,
            IEmailService emailService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<StudioService> logger,
            IConfiguration configuration)
        {
            _studioRepository = studioRepository;
            _groupRepository = groupRepository;
            _userSubscriptionRepository = userSubscriptionRepository;
            _studioParticipantRepository = studioParticipantRepository;
            _groupParticipantRepository = groupParticipantRepository;
            _userRepository = userRepository;
            _emailService = emailService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _configuration = configuration;
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
                MemberCount = 0,
                StartDate = studio.StartDate,
                EndDate = studio.EndDate,
                AvatarUrl = studio.AvatarUrl,
                ColorHex = studio.ColorHex,
                BannerUrl = studio.BannerUrl,
                Tagline = studio.Tagline,
                Alias = studio.Alias,
                IsOpen = studio.IsOpen
            }).ToList();

            foreach (var response in studioResponses)
            {
                response.GroupCount = await _groupRepository.GetGroupCountByStudioIdAsync(response.StudioId);
                response.MemberCount = await _studioParticipantRepository.GetParticipantCountByStudioIdAsync(response.StudioId);
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

            if (studio.StudioName.Length > 255)
            {
                throw new AppException(ErrorCodes.StudioNameInvalid, StatusCodes.Status400BadRequest);
            }

            if (studio.Description != null && studio.Description.Length > 500)
            {
                throw new AppException(ErrorCodes.StudioDescriptionInvalid, StatusCodes.Status400BadRequest);
            }

            var isStudioNameExist = await _studioRepository.IsStudioNameExistByOwnerIdAsync(studio.StudioName, ownerId);
            if (isStudioNameExist)
            {
                throw new AppException(ErrorCodes.StudioNameAlreadyExist, StatusCodes.Status400BadRequest);
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
                EndDate = studio.EndDate.HasValue ? DateTime.SpecifyKind(studio.EndDate.Value, DateTimeKind.Utc) : null,
                // 🔹 FIX + ADD: Studio personalization
                AvatarUrl = studio.AvatarUrl,
                ColorHex = studio.ColorHex,
                BannerUrl = studio.BannerUrl,
                Tagline = studio.Tagline,
                Alias = studio.Alias,
                // 🔹 ADDED: IsOpen setting
                IsOpen = studio.IsOpen
            };

            await _studioRepository.CreateStudioAsync(createStudio);

            // Auto-set creator as Owner in StudioParticipant (always approved)
            var studioParticipant = new StudioParticipant
            {
                ParticipantId = Guid.NewGuid(),
                StudioId = createStudio.StudioId,
                UserId = ownerId,
                Role = StudioRole.Owner,
                IsApproved = true,
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
                MemberCount = 1,
                StartDate = createStudio.StartDate,
                EndDate = createStudio.EndDate,
                AvatarUrl = createStudio.AvatarUrl,
                ColorHex = createStudio.ColorHex,
                BannerUrl = createStudio.BannerUrl,
                Tagline = createStudio.Tagline,
                Alias = createStudio.Alias,
                IsOpen = createStudio.IsOpen
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
                MemberCount = await _studioParticipantRepository.GetParticipantCountByStudioIdAsync(studio.StudioId),
                StartDate = studio.StartDate,
                EndDate = studio.EndDate,
                AvatarUrl = studio.AvatarUrl,
                ColorHex = studio.ColorHex,
                BannerUrl = studio.BannerUrl,
                Tagline = studio.Tagline,
                Alias = studio.Alias,
                IsOpen = studio.IsOpen
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

            if (studio.StudioName.Length > 255)
            {
                throw new AppException(ErrorCodes.StudioNameInvalid, StatusCodes.Status400BadRequest);
            }

            if (studio.Description != null && studio.Description.Length > 500)
            {
                throw new AppException(ErrorCodes.StudioDescriptionInvalid, StatusCodes.Status400BadRequest);
            }

            var isStudioNameExist = await _studioRepository.IsStudioNameExistExcludingStudioAsync(
                studio.StudioName, userId, studio.Id);
            if (isStudioNameExist)
            {
                throw new AppException(ErrorCodes.StudioNameAlreadyExist, StatusCodes.Status400BadRequest);
            }

            updateStudio.StudioName = studio.StudioName;
            updateStudio.Description = studio.Description;
            // Normalize to UTC for PostgreSQL timestamp with time zone compatibility
            updateStudio.StartDate = studio.StartDate.HasValue ? DateTime.SpecifyKind(studio.StartDate.Value, DateTimeKind.Utc) : null;
            updateStudio.EndDate = studio.EndDate.HasValue ? DateTime.SpecifyKind(studio.EndDate.Value, DateTimeKind.Utc) : null;

            // 🔹 ADDED: Validate and update personalization fields
            if (!string.IsNullOrEmpty(studio.ColorHex))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(studio.ColorHex, @"^#[0-9A-Fa-f]{6}$", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromMilliseconds(200)))
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

            // 🔹 ADDED: Validate and update BannerUrl
            if (!string.IsNullOrEmpty(studio.BannerUrl) && !Uri.TryCreate(studio.BannerUrl, UriKind.Absolute, out _))
            {
                throw new AppException(ErrorCodes.ValidationInvalidBannerUrl, StatusCodes.Status400BadRequest);
            }
            updateStudio.BannerUrl = studio.BannerUrl;

            // 🔹 ADDED: Validate and update Tagline
            if (!string.IsNullOrEmpty(studio.Tagline) && studio.Tagline.Length > 200)
            {
                throw new AppException(ErrorCodes.ValidationStringLength, StatusCodes.Status400BadRequest);
            }
            updateStudio.Tagline = studio.Tagline;

            // 🔹 ADDED: Validate and update Alias
            if (!string.IsNullOrEmpty(studio.Alias))
            {
                if (studio.Alias.Length > 50)
                {
                    throw new AppException(ErrorCodes.ValidationStringLength, StatusCodes.Status400BadRequest);
                }
                if (!System.Text.RegularExpressions.Regex.IsMatch(studio.Alias, @"^[a-zA-Z0-9\sÀ-ỹ_\-]+$", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromMilliseconds(200)))
                {
                    throw new AppException(ErrorCodes.ValidationInvalidAlias, StatusCodes.Status400BadRequest);
                }
                updateStudio.Alias = studio.Alias;
            }
            else if (studio.Alias == null)
            {
                updateStudio.Alias = null;
            }

            // 🔹 ADDED: Update IsOpen setting (Owner only)
            if (studio.IsOpen.HasValue)
            {
                updateStudio.IsOpen = studio.IsOpen.Value;
            }

            await _studioRepository.UpdateStudioAsync(updateStudio);

            return new UpdateStudioResponse
            {
                StudioName = updateStudio.StudioName,
                Description = updateStudio.Description,
                UpdatedAt = updateStudio.UpdatedAt,
                StartDate = updateStudio.StartDate,
                EndDate = updateStudio.EndDate,
                AvatarUrl = updateStudio.AvatarUrl,
                ColorHex = updateStudio.ColorHex,
                BannerUrl = updateStudio.BannerUrl,
                Tagline = updateStudio.Tagline,
                Alias = updateStudio.Alias,
                IsOpen = updateStudio.IsOpen
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
                    IsApproved = participant.IsApproved,
                    GroupInfo = groupInfoList
                });
            }

            return result;
        }

        /// <summary>
        /// Leave a studio (self-remove)
        /// Also leaves all groups in the studio that the user is a member of
        /// Validate:
        /// - Studio must exist
        /// - User must be a member of the studio
        /// - Owner cannot leave
        /// </summary>
        public async Task<LeaveStudioResponse> LeaveStudioAsync(Guid userId, Guid studioId)
        {
            var studio = await _studioRepository.GetByIdAsync(studioId);
            if (studio == null)
            {
                throw new AppException(ErrorCodes.StudioNotFound, StatusCodes.Status404NotFound);
            }

            var participant = await _studioParticipantRepository.GetByStudioAndUserIncludeNonApprovedAsync(studioId, userId);
            if (participant == null)
            {
                throw new AppException(ErrorCodes.StudioNotFound, StatusCodes.Status404NotFound);
            }

            if (studio.OwnerId == userId)
            {
                throw new AppException(ErrorCodes.StudioCannotLeaveAsOwner, StatusCodes.Status403Forbidden);
            }

            // Remove user from all groups in this studio
            var studioGroups = await _groupRepository.GetStudioGroupsAsync(studioId);
            if (studioGroups.Any())
            {
                var groupIds = studioGroups.Select(g => g.GroupId).ToList();
                var groupParticipants = await _groupParticipantRepository.GetByGroupIdsAsync(groupIds);
                var userGroupParticipants = groupParticipants
                    .Where(gp => gp.UserId == userId && gp.Role != GroupRole.Owner)
                    .ToList();

                if (userGroupParticipants.Any())
                {
                    await _groupParticipantRepository.RemoveRangeAsync(userGroupParticipants);
                }
            }

            await _studioParticipantRepository.RemoveAsync(participant);

            return new LeaveStudioResponse
            {
                StudioId = studio.StudioId,
                StudioName = studio.StudioName,
                LeftAt = DateTime.UtcNow
            };
        }

        // 🔹 ADDED: Toggle IsOpen setting (Owner only)
        public async Task<ToggleIsOpenResponse> ToggleIsOpenAsync(Guid userId, Guid studioId, bool isOpen)
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

            studio.IsOpen = isOpen;
            studio.UpdatedAt = DateTime.UtcNow;
            await _studioRepository.UpdateStudioAsync(studio);

            _logger.LogInformation(
                "User {UserId} toggled IsOpen to {IsOpen} for studio {StudioId}",
                userId, isOpen, studioId);

            return new ToggleIsOpenResponse
            {
                Id = studio.StudioId,
                Name = studio.StudioName,
                IsOpen = studio.IsOpen,
                UpdatedAt = studio.UpdatedAt
            };
        }

        // 🔹 ADDED: Get pending members (Owner only)
        public async Task<StudioPendingMemberListResponse> GetPendingMembersAsync(Guid userId, Guid studioId)
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

            var pendingParticipants = await _studioParticipantRepository.GetPendingByStudioIdAsync(studioId);

            var pendingMembers = pendingParticipants.Select(p =>
            {
                return new StudioPendingMemberDto
                {
                    UserId = p.UserId,
                    FirstName = p.User?.FirstName ?? "",
                    LastName = p.User?.LastName ?? "",
                    Email = p.User?.Email ?? "",
                    AvatarUrl = AvatarUrlHelper.BuildAbsoluteAvatarUrl(p.User?.AvatarUrl, _httpContextAccessor.HttpContext),
                    RequestedAt = p.CreatedAt
                };
            }).ToList();

            return new StudioPendingMemberListResponse
            {
                StudioId = studio.StudioId,
                StudioName = studio.StudioName,
                TotalPending = pendingMembers.Count,
                PendingMembers = pendingMembers
            };
        }

        /// <summary>
        /// Remove member from studio
        /// Validate:
        /// - Studio must exist
        /// - Current user must be Owner
        /// - Cannot remove yourself
        /// - Cannot remove Owner
        /// </summary>
        public async Task<RemoveStudioMemberResponse> RemoveMemberAsync(
            Guid currentUserId,
            RemoveStudioMemberRequest request)
        {
            // Validate: Studio must exist
            var studio = await _studioRepository.GetByIdAsync(request.StudioId);
            if (studio == null)
            {
                throw new AppException(ErrorCodes.StudioNotFound, StatusCodes.Status404NotFound);
            }

            // Validate: Current user must be Owner of the studio
            if (studio.OwnerId != currentUserId)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            // Cannot remove yourself
            if (request.UserId == currentUserId)
            {
                throw new AppException(
                    ErrorCodes.StudioCannotRemoveSelf,
                    StatusCodes.Status400BadRequest);
            }

            // Get target member record (tracked)
            var targetMember = await _studioParticipantRepository
                .GetByStudioAndUserTrackedAsync(request.StudioId, request.UserId);

            if (targetMember == null)
            {
                throw new AppException(
                    ErrorCodes.StudioMemberNotFound,
                    StatusCodes.Status404NotFound);
            }

            // Cannot remove Owner
            if (targetMember.Role == StudioRole.Owner)
            {
                throw new AppException(
                    ErrorCodes.StudioCannotRemoveOwner,
                    StatusCodes.Status400BadRequest);
            }

            // Remove user from all groups in this studio first
            var studioGroups = await _groupRepository.GetStudioGroupsAsync(request.StudioId);
            if (studioGroups.Count > 0)
            {
                var groupIds = studioGroups.Select(g => g.GroupId).ToList();
                var groupParticipants = await _groupParticipantRepository.GetByGroupIdsAsync(groupIds);
                var userGroupParticipants = groupParticipants
                    .Where(gp => gp.UserId == request.UserId)
                    .ToList();

                if (userGroupParticipants.Count > 0)
                {
                    await _groupParticipantRepository.RemoveRangeAsync(userGroupParticipants);
                }
            }

            // Get removed user info before deletion
            var removedUser = await _userRepository.GetByIdAsync(request.UserId);

            // Remove member from studio
            await _studioParticipantRepository.RemoveAsync(targetMember);

            _logger.LogInformation(
                "User {UserId} removed user {RemovedUserId} from studio {StudioId}",
                currentUserId, request.UserId, request.StudioId);

            return new RemoveStudioMemberResponse
            {
                StudioId = request.StudioId,
                StudioName = studio.StudioName,
                RemovedUserId = request.UserId,
                RemovedUserName = removedUser != null
                    ? $"{removedUser.FirstName} {removedUser.LastName}"
                    : "Unknown User",
                RemovedAt = DateTime.UtcNow
            };
        }
        // Approve pending member (Owner only)
        public async Task<ApproveMemberResponse> ApproveMemberAsync(Guid userId, Guid studioId, Guid targetUserId)
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

            var targetParticipant = await _studioParticipantRepository.GetPendingByStudioAndUserAsync(studioId, targetUserId);
            if (targetParticipant == null)
            {
                throw new AppException(ErrorCodes.StudioMemberNotFound, StatusCodes.Status404NotFound);
            }

            targetParticipant.IsApproved = true;
            await _studioParticipantRepository.UpdateAsync(targetParticipant);

            // Auto-approve user in all studio groups if they were added as pending
            var studioGroups = await _groupRepository.GetStudioGroupsAsync(studioId);
            var groupIds = studioGroups.Select(g => g.GroupId).ToList();

            if (groupIds.Any())
            {
                var existingGroupParticipants = await _groupParticipantRepository.GetByGroupIdsAsync(groupIds);

                foreach (var group in studioGroups)
                {
                    var pendingInGroup = existingGroupParticipants
                        .FirstOrDefault(p => p.GroupId == group.GroupId && p.UserId == targetUserId && !p.IsApproved);

                    if (pendingInGroup != null)
                    {
                        // User was added as pending when joining the group - now approve them
                        pendingInGroup.IsApproved = true;
                        await _groupParticipantRepository.UpdateAsync(pendingInGroup);

                        _logger.LogInformation(
                            "User {TargetUserId} auto-approved in group {GroupId} after studio {StudioId} approval",
                            targetUserId, group.GroupId, studioId);
                    }
                    else
                    {
                        // Check if user is already an approved member or doesn't exist
                        var isGroupMember = await _groupParticipantRepository
                            .IsUserApprovedInGroupAsync(group.GroupId, targetUserId);

                        if (!isGroupMember && !existingGroupParticipants.Any(p => p.GroupId == group.GroupId && p.UserId == targetUserId))
                        {
                            // User not in group at all - add them if group is open (auto-join groups)
                            if (group.IsOpen)
                            {
                                var groupParticipant = new GroupParticipant
                                {
                                    ParticipantId = Guid.NewGuid(),
                                    GroupId = group.GroupId,
                                    UserId = targetUserId,
                                    Role = GroupRole.Member,
                                    IsApproved = true,
                                    CreatedAt = DateTime.UtcNow
                                };

                                try
                                {
                                    await _groupParticipantRepository.AddAsync(groupParticipant);

                                    _logger.LogInformation(
                                        "User {TargetUserId} auto-added to group {GroupId} after studio {StudioId} approval",
                                        targetUserId, group.GroupId, studioId);
                                }
                                catch (DbUpdateException)
                                {
                                    // User already in group (race condition), ignore
                                    _logger.LogWarning(
                                        "User {TargetUserId} already in group {GroupId} when approving studio {StudioId}",
                                        targetUserId, group.GroupId, studioId);
                                }
                            }
                        }
                    }
                }
            }

            var targetUser = await _userRepository.GetByIdAsync(targetUserId);

            // Send approval notification email
            if (targetUser != null && !string.IsNullOrEmpty(targetUser.Email))
            {
                try
                {
                    var targetEmail = targetUser.Email;
                    var approvalUrl = BuildStudioUrl(studioId);
                    var language = targetUser.Language == "vi" ? Language.Vietnamese : Language.English;
                    string nameToShow = targetUser.Language == "vi" ? $"quản lý {studio.StudioName}" : $"studio {studio.StudioName}";

                    var emailBody = EmailTemplate.MemberApprovedNotification(
                        nameToShow,
                        approvalUrl,
                        DateTime.UtcNow,
                        language);

                    await _emailService.SendLinkAsync(
                        targetEmail,
                        "Join studio request approved",
                        emailBody);

                    _logger.LogInformation(
                        "Approval notification email sent to {Email} for studio {StudioId}",
                        targetUser.Email, studioId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to send approval notification email to {Email}",
                        targetUser.Email);
                }
            }

            _logger.LogInformation(
                "User {UserId} approved member {TargetUserId} in studio {StudioId}",
                userId, targetUserId, studioId);

            return new ApproveMemberResponse
            {
                Id = studio.StudioId,
                Name = studio.StudioName,
                UserId = targetUserId,
                UserName = $"{targetUser?.FirstName} {targetUser?.LastName}",
                IsApproved = true,
                UpdatedAt = DateTime.UtcNow
            };
        }
        private string BuildStudioUrl(Guid studioId)
        {
            var baseUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
            return $"{baseUrl}/master/{studioId}";
        }
    }
}
