using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Configurations;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using StudioStudio_Server.Utils;

namespace StudioStudio_Server.Services
{
    public class GroupService(
        ILogger<GroupService> logger,
        IGroupRepository groupRepository,
        IUserSubscriptionRepository userSubscriptionRepository,
        IFavouriteRepository favouriteRepository,
        IUserRepository userRepository,
        IStudioRepository studioRepository,
        IGroupParticipantRepository groupParticipantRepository,
        ITaskRepository taskRepository,
        ITemplateRepository templateRepository,
        IGroupTaskStatusRepository groupTaskStatusRepository,
        ITaskAssignmentRepository taskAssignmentRepository,
        IStudioParticipantRepository studioParticipantRepository,
        IEmailService emailService,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        ICacheService cacheService) : IGroupService
    {
        private static readonly string[] BrandColors = new[]
        {
            "#FF5F3D", "#FF7A54", "#FF4D6A", "#FF3CAC",
            "#7C3AED", "#4F46E5", "#2563EB", "#06B6D4",
            "#10B981", "#84CC16", "#F59E0B", "#F43F5E"
        };

        /// <summary>
        /// Get all groups that the current user can access and split them by UI sections.
        /// Validate: <paramref name="userId"/> must belong to an existing non-deleted user.
        /// Returns: Group list response with subscription summary and categorized cards.
        /// </summary>
        public async Task<GroupListResponse> GetGroupsAsync(Guid userId)
        {
            // Get user's subscription plan
            var subscriptionPlan = await userSubscriptionRepository.GetSubscriptionPlanByUserIdAsync(userId);

            // Get all groups user participates in
            var groups = await groupRepository.GetUserGroupsAsync(userId);

            // Get all group IDs
            var groupIds = groups.Select(g => g.GroupId).ToList();

            // Get favorites for this user
            var favorites = await favouriteRepository.GetByUserAndGroupIdsAsync(userId, groupIds);
            var favoriteGroupIds = favorites.Select(f => f.GroupId).ToHashSet();

            // Get all creators
            var creatorIds = groups.Select(g => g.CreatedBy).Distinct().ToList();
            var creators = await userRepository.GetByIdsAsync(creatorIds);

            // Get studios
            var studioIds = groups.Where(g => g.StudioId.HasValue)
                .Select(g => g.StudioId.Value).Distinct().ToList();
            var studios = await studioRepository.GetByIdsAsync(studioIds);

            // Get all participants for member previews
            var allParticipants = await groupParticipantRepository.GetByGroupIdsAsync(groupIds);

            var participantUserIds = allParticipants.Select(gp => gp.UserId).Distinct().ToList();
            var users = await userRepository.GetByIdsAsync(participantUserIds);

            // Get task counts
            var taskCounts = await taskRepository.GetTaskCountByGroupIdsAsync(groupIds);

            // Build GroupCardDto for all groups
            var groupCards = groups.Select(g =>
            {
                var userRole = g.Participants.FirstOrDefault(p => p.UserId == userId)?.Role ?? GroupRole.Viewer;
                var createdByUser = creators.FirstOrDefault(u => u.UserId == g.CreatedBy);
                var studio = g.StudioId.HasValue ? studios.FirstOrDefault(s => s.StudioId == g.StudioId) : null;

                var groupParticipants = allParticipants.Where(gp => gp.GroupId == g.GroupId).ToList();

                var membersPreview = groupParticipants
                    .Take(5)
                    .Select(gp =>
                    {
                        var user = users.FirstOrDefault(u => u.UserId == gp.UserId);
                        return new MemberPreviewDto
                        {
                            Id = gp.UserId,
                            FirstName = user?.FirstName ?? "",
                            LastName = user?.LastName ?? "",
                            AvatarUrl = AvatarUrlHelper.BuildAbsoluteAvatarUrl(user?.AvatarUrl, httpContextAccessor.HttpContext)
                        };
                    })
                    .ToList();

                return new GroupCardDto
                {
                    Id = g.GroupId,
                    Name = g.GroupName,
                    Description = g.Description ?? "",
                    IsFavorite = favoriteGroupIds.Contains(g.GroupId),
                    Role = userRole.ToString(),
                    Studio = studio != null ? new StudioDto
                    {
                        Id = studio.StudioId,
                        Name = studio.StudioName
                    } : null,
                    CreatedBy = createdByUser != null ? new UserDto
                    {
                        Id = createdByUser.UserId,
                        FirstName = createdByUser.FirstName,
                        LastName = createdByUser.LastName,
                        AvatarUrl = AvatarUrlHelper.BuildAbsoluteAvatarUrl(createdByUser.AvatarUrl, httpContextAccessor.HttpContext)
                    } : new UserDto(),
                    MemberCount = groupParticipants.Count,
                    TaskCount = taskCounts.TryGetValue(g.GroupId, out var count) ? count : 0,
                    LastActivityAt = g.UpdatedAt,
                    MembersPreview = membersPreview,
                    AvatarUrl = g.AvatarUrl,
                    ColorHex = g.ColorHex,
                    IconEmoji = g.IconEmoji,
                    BannerUrl = g.BannerUrl,
                    Tagline = g.Tagline,
                    Alias = g.Alias,
                    IsOpen = g.IsOpen,
                    IsArchived = g.IsArchived,
                    IsMember = groupParticipants.Any(p => p.UserId == userId && p.IsApproved),
                    AllowMemberUpdateProgress = g.AllowMemberUpdateProgress
                };
            }).ToList();

            // Categorize groups
            var archivedGroups = groupCards.Where(g => g.IsArchived).ToList();
            var activeGroups = groupCards.Where(g => !g.IsArchived).ToList();

            var favoriteGroups = activeGroups.Where(g => g.IsFavorite).ToList();
            var studioGroups = activeGroups.Where(g => g.Studio != null).ToList();
            var independentGroups = activeGroups.Where(g => g.Studio == null).ToList();

            // Count groups created by user (where user is Owner)
            var groupsCreatedByUser = groups.Count(g =>
                g.Participants.Any(p => p.Role == GroupRole.Owner && p.UserId == userId));

            var response = new GroupListResponse
            {
                Subscription = new SubscriptionInfo
                {
                    GroupLimit = subscriptionPlan?.MaxGroups ?? 5,
                    GroupCreated = groupsCreatedByUser,
                    MemberLimit = subscriptionPlan?.MaxMembersPerGroup ?? 10
                },
                Summary = new GroupSummary
                {
                    TotalGroups = groups.Count,
                    FavoriteCount = favoriteGroups.Count,
                    StudioGroupCount = studioGroups.Count,
                    IndependentGroupCount = independentGroups.Count,
                    ArchivedCount = archivedGroups.Count
                },
                Sections = new GroupSections
                {
                    Favorites = favoriteGroups,
                    StudioGroups = studioGroups,
                    IndependentGroups = independentGroups,
                    ArchivedGroups = archivedGroups
                }
            };

            return response;
        }

        /// <summary>
        /// Get full detail for one group that the caller is an approved member of.
        /// Validate: <paramref name="userId"/> must be an approved participant of <paramref name="groupId"/>.
        /// Returns: Group detail response including participants, statuses and counters.
        /// </summary>
        public async Task<GroupDetailResponse> GetGroupDetailAsync(Guid userId, Guid groupId)
        {
            // Get group with participants
            var group = await groupRepository.GetGroupWithDetailsAsync(groupId);
            if (group == null)
            {
                throw new AppException(ErrorCodes.GroupNotFound, StatusCodes.Status404NotFound);
            }

            // Check if user is an approved member
            var userParticipant = group.Participants.FirstOrDefault(p => p.UserId == userId);
            if (userParticipant == null || !userParticipant.IsApproved)
            {
                throw new AppException(ErrorCodes.GroupAccessDenied, StatusCodes.Status403Forbidden);
            }

            // Get studio info if group belongs to a studio
            Studio? studio = null;
            if (group.StudioId.HasValue)
            {
                studio = await studioRepository.GetByIdAsync(group.StudioId.Value);
            }

            // Get creator info
            var creator = await userRepository.GetByIdAsync(group.CreatedBy);

            // Check if favorite
            var isFavorite = await favouriteRepository.IsFavouriteAsync(userId, groupId);

            // Get task count
            var taskCount = await taskRepository.GetTaskCountByGroupIdAsync(groupId);

            // Get member count
            var memberCount = await groupParticipantRepository.GetParticipantCountByGroupIdAsync(groupId);

            // Get task statuses
            var taskStatuses = await groupTaskStatusRepository.GetByGroupIdAsync(groupId);

            // Get tasks
            var taskStatusIdList = taskStatuses.Select(s => s.StatusId).ToList();
            var taskList = await taskRepository.GetListTasksByListStatusId(taskStatusIdList);

            // Get assinees
            var taskIdList = taskList.SelectMany(t => t.Value).Select(t => t.TaskId).ToList();

            Dictionary<Guid, UserDto> assigneeDict = new();
            if (taskIdList.Count > 0)
            {
                var assignees = await taskAssignmentRepository.GetListAssigneesByListTaskId(taskIdList);
                var userIds = assignees.Select(a => a.AssignedTo).Distinct().ToList();
                var users = await userRepository.GetByIdsAsync(userIds);
                var userDict = users.ToDictionary(u => u.UserId);

                foreach (var assignee in assignees)
                {
                    if (userDict.TryGetValue(assignee.AssignedTo, out var user))
                    {
                        assigneeDict[assignee.TaskId] = new UserDto
                        {
                            Id = user.UserId,
                            FirstName = user.FirstName,
                            LastName = user.LastName,
                            AvatarUrl = AvatarUrlHelper.BuildAbsoluteAvatarUrl(user.AvatarUrl, httpContextAccessor.HttpContext)
                        };
                    }
                }
            }


            // Check if group is a template
            var template = await templateRepository.GetByGroupIdAsync(groupId);

            return new GroupDetailResponse
            {
                GroupId = group.GroupId,
                GroupName = group.GroupName,
                Description = group.Description,
                StudioId = group.StudioId,
                StudioName = studio?.StudioName,
                GroupType = group.StudioId.HasValue ? "Studio" : "Independent",
                IsFavorite = isFavorite,
                IsTemplate = template != null && template.IsActive,
                TemplateId = template?.TemplateId,
                CreatedBy = creator != null ? new UserDto
                {
                    Id = creator.UserId,
                    FirstName = creator.FirstName,
                    LastName = creator.LastName,
                    AvatarUrl = AvatarUrlHelper.BuildAbsoluteAvatarUrl(creator.AvatarUrl, httpContextAccessor.HttpContext)
                } : new UserDto(),
                CreatedAt = group.CreatedAt,
                UpdatedAt = group.UpdatedAt,
                MemberCount = memberCount,
                TaskCount = taskCount,
                UserRole = userParticipant.Role.ToString(),
                AvatarUrl = group.AvatarUrl,
                ColorHex = group.ColorHex,
                IconEmoji = group.IconEmoji,
                BannerUrl = group.BannerUrl,
                Tagline = group.Tagline,
                Alias = group.Alias,
                IsOpen = group.IsOpen,
                IsArchived = group.IsArchived,
                AllowMemberUpdateProgress = group.AllowMemberUpdateProgress,
                TaskStatuses = taskStatuses.Select(ts => new TaskStatusDto
                {
                    StatusId = ts.StatusId,
                    StatusName = ts.StatusName,
                    Position = ts.Position,
                    TaskList = taskList.TryGetValue(ts.StatusId, out var tasks)
                        ? tasks.Select(t => new TaskItemResponse
                        {
                            TaskId = t.TaskId,
                            TaskTitle = t.Title,
                            TaskDescription = t.Description!,
                            TaskPriority = t.Priority,
                            TaskSeverity = t.Severity,
                            Position = t.Position,
                            Progress = t.Progress,
                            CreatedById = t.OwnerId,
                            CreatedAt = t.CreatedAt,
                            StartDate = t.StartDate,
                            DueDate = t.DueDate,
                            EstimatedHours = t.EstimatedHours,
                            ActualHours = t.ActualHours,
                            CompletedAt = t.CompletedAt,
                            Assignee = assigneeDict.TryGetValue(t.TaskId, out var assignee) ? assignee : null
                        }).ToList()
            : new List<TaskItemResponse>()
                }).ToList()
            };
        }

        public async Task<GroupMemberListResponse> GetGroupMembersAsync(Guid userId, Guid groupId)
        {
            // Get group
            var group = await groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new AppException(ErrorCodes.GroupNotFound, StatusCodes.Status404NotFound);
            }

            // Check if user is an approved member
            var isUserInGroup = await groupParticipantRepository.IsUserApprovedInGroupAsync(groupId, userId);
            if (!isUserInGroup)
            {
                throw new AppException(ErrorCodes.GroupPermissionDenied, StatusCodes.Status403Forbidden);
            }

            // Get all participants (including pending for the pending members list API)
            var participants = await groupParticipantRepository.GetAllByGroupIdAsync(groupId);

            // Get user info for all participants
            var userIds = participants.Select(p => p.UserId).ToList();
            var users = await userRepository.GetByIdsAsync(userIds);

            // Build member list
            var members = participants.Select(p =>
            {
                var user = users.FirstOrDefault(u => u.UserId == p.UserId);
                return new GroupMemberDto
                {
                    UserId = p.UserId,
                    FirstName = user?.FirstName ?? "",
                    LastName = user?.LastName ?? "",
                    Email = user?.Email ?? "",
                    AvatarUrl = AvatarUrlHelper.BuildAbsoluteAvatarUrl(user?.AvatarUrl, httpContextAccessor.HttpContext),
                    Role = p.Role.ToString(),
                    IsApproved = p.IsApproved,
                    JoinedAt = p.CreatedAt
                };
            })
            .OrderBy(m => m.Role == "Owner" ? 0 : m.Role == "Moderator" ? 1 : 2)
            .ThenBy(m => m.JoinedAt)
            .ToList();

            return new GroupMemberListResponse
            {
                GroupId = group.GroupId,
                GroupName = group.GroupName,
                TotalMembers = members.Count,
                Members = members
            };
        }

        public async Task<CreateGroupResponse> CreateGroupAsync(Guid userId, CreateGroupRequest request)
        {
            // Check subscription limit
            var subscriptionPlan = await userSubscriptionRepository.GetSubscriptionPlanByUserIdAsync(userId);
            var groupLimit = subscriptionPlan?.MaxGroups ?? 5;

            var currentGroupCount = await groupRepository.CountGroupsCreatedByUserAsync(userId);
            if (currentGroupCount >= groupLimit)
            {
                throw new AppException(ErrorCodes.GroupLimitReached, StatusCodes.Status403Forbidden);
            }

            // Validate: GroupName must not exceed 255 characters
            if (request.GroupName.Length > 255)
            {
                throw new AppException(ErrorCodes.GroupNameInvalid);
            }

            // Validate: Description must not exceed 500 characters
            if (request.Description != null && request.Description.Length > 500)
            {
                throw new AppException(ErrorCodes.GroupDescriptionInvalid);
            }

            // Check if creating in studio
            if (request.StudioId.HasValue)
            {
                // Verify studio exists
                var studio = await studioRepository.GetByIdAsync(request.StudioId.Value);
                if (studio == null)
                {
                    throw new AppException(ErrorCodes.StudioNotFound, StatusCodes.Status404NotFound);
                }

                // Verify user is studio owner
                var isOwner = await studioRepository.IsUserStudioOwnerAsync(request.StudioId.Value, userId);
                if (!isOwner)
                {
                    throw new AppException(ErrorCodes.StudioPermissionDenied, StatusCodes.Status403Forbidden);
                }

                // Check if group name already exists in this studio
                var nameExists = await groupRepository.GroupNameExistsInStudioAsync(request.StudioId, request.GroupName, null);
                if (nameExists)
                {
                    throw new AppException(ErrorCodes.GroupNameAlreadyExists);
                }
            }
            else
            {
                // For independent groups, check if name exists (studioId = null)
                var nameExists = await groupRepository.GroupNameExistsInStudioAsync(null, request.GroupName, userId);
                if (nameExists)
                {
                    throw new AppException(ErrorCodes.GroupPersonalAlreadyExists);
                }
            }

            // Validate template if provided
            List<GroupTaskStatus>? templateTaskStatuses = null;
            if (request.TemplateId.HasValue)
            {
                var template = await templateRepository.GetByIdAsync(request.TemplateId.Value);
                if (template == null)
                {
                    throw new AppException(ErrorCodes.TemplateNotFound, StatusCodes.Status404NotFound);
                }

                // Check if user has access to this template
                if (!template.IsSystemTemplate && template.UserId != userId)
                {
                    throw new AppException(ErrorCodes.TemplatePermissionDenied, StatusCodes.Status403Forbidden);
                }

                // Get template's task statuses
                templateTaskStatuses = await groupTaskStatusRepository.GetByGroupIdAsync(template.GroupId);
            }

            // Create new group
            var now = DateTime.UtcNow;

            // validate personalization fields
            if (!string.IsNullOrEmpty(request.ColorHex))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(request.ColorHex, @"^#[0-9A-Fa-f]{6}$", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromMilliseconds(200)))
                {
                    throw new AppException(ErrorCodes.ValidationInvalidColor);
                }
            }

            if (!string.IsNullOrEmpty(request.IconEmoji) && request.IconEmoji.Length > 10)
            {
                throw new AppException(ErrorCodes.ValidationInvalidEmoji);
            }

            // validate BannerUrl
            if (!string.IsNullOrEmpty(request.BannerUrl) && !Uri.TryCreate(request.BannerUrl, UriKind.Absolute, out _))
            {
                throw new AppException(ErrorCodes.ValidationInvalidBannerUrl);
            }

            // validate Tagline
            if (!string.IsNullOrEmpty(request.Tagline) && request.Tagline.Length > 200)
            {
                throw new AppException(ErrorCodes.ValidationStringLength);
            }

            // validate Alias pattern
            if (!string.IsNullOrEmpty(request.Alias))
            {
                if (request.Alias.Length > 50)
                {
                    throw new AppException(ErrorCodes.ValidationStringLength);
                }
                if (!System.Text.RegularExpressions.Regex.IsMatch(request.Alias, @"^[a-zA-Z0-9\sÀ-ỹ_\-]+$", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromMilliseconds(200)))
                {
                    throw new AppException(ErrorCodes.ValidationInvalidAlias);
                }
            }

            var newGroup = new Group
            {
                GroupId = Guid.NewGuid(),
                GroupName = request.GroupName,
                Description = request.Description,
                StudioId = request.StudioId,
                CreatedBy = userId,
                IsTemplate = false,
                IsActive = true,
                IsOpen = request.IsOpen,
                CreatedAt = now,
                UpdatedAt = now,
                AvatarUrl = request.AvatarUrl,
                ColorHex = request.ColorHex,
                IconEmoji = request.IconEmoji,
                BannerUrl = request.BannerUrl,
                Tagline = request.Tagline,
                Alias = request.Alias,
                IsArchived = false
            };

            await groupRepository.AddAsync(newGroup);

            // Add creator as Owner participant (always approved)
            var ownerParticipant = new GroupParticipant
            {
                ParticipantId = Guid.NewGuid(),
                GroupId = newGroup.GroupId,
                UserId = userId,
                Role = GroupRole.Owner,
                IsApproved = true,
                CreatedAt = now
            };

            await groupParticipantRepository.AddAsync(ownerParticipant);

            // Copy task statuses from template if provided
            if (templateTaskStatuses != null && templateTaskStatuses.Any())
            {
                var newTaskStatuses = templateTaskStatuses.Select(ts => new GroupTaskStatus
                {
                    StatusId = Guid.NewGuid(),
                    GroupId = newGroup.GroupId,
                    StatusName = ts.StatusName,
                    Position = ts.Position
                }).ToList();

                await groupTaskStatusRepository.AddRangeAsync(newTaskStatuses);
            }

            return new CreateGroupResponse
            {
                GroupId = newGroup.GroupId,
                GroupName = newGroup.GroupName,
                Description = newGroup.Description,
                StudioId = newGroup.StudioId,
                GroupType = newGroup.StudioId.HasValue ? "Studio" : "Independent",
                CreatedBy = userId,
                CreatedAt = newGroup.CreatedAt,
                AvatarUrl = newGroup.AvatarUrl,
                ColorHex = newGroup.ColorHex,
                IconEmoji = newGroup.IconEmoji,
                BannerUrl = newGroup.BannerUrl,
                Tagline = newGroup.Tagline,
                Alias = newGroup.Alias,
                IsOpen = newGroup.IsOpen,
                IsArchived = newGroup.IsArchived
            };
        }

        public async Task DeleteGroupAsync(Guid userId, Guid groupId)
        {
            // Check if group exists
            var group = await groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new AppException(ErrorCodes.GroupNotFound, StatusCodes.Status404NotFound);
            }

            // Check if user is the owner of the group
            var isOwner = await groupRepository.IsUserGroupOwnerAsync(groupId, userId);
            if (!isOwner)
            {
                throw new AppException(ErrorCodes.GroupPermissionDenied, StatusCodes.Status403Forbidden);
            }

            // Soft delete the group
            await groupRepository.DeleteAsync(group);
        }

        /// <summary>
        /// Update group metadata and optional template activation state.
        /// Validate: caller must be Owner/Moderator and request payload must pass field constraints.
        /// Returns: Updated group snapshot for client refresh.
        /// </summary>
        public async Task<UpdateGroupResponse> UpdateGroupAsync(Guid userId, UpdateGroupRequest request)
        {
            // Check if group exists
            var group = await groupRepository.GetByIdAsync(request.GroupId);
            if (group == null)
            {
                throw new AppException(ErrorCodes.GroupNotFound, StatusCodes.Status404NotFound);
            }

            // Check if user is Owner or Moderator
            var userParticipant = await groupParticipantRepository.GetByGroupAndUserAsync(request.GroupId, userId);
            if (userParticipant == null ||
                (userParticipant.Role != GroupRole.Owner && userParticipant.Role != GroupRole.Moderator))
            {
                throw new AppException(ErrorCodes.GroupUpdatePermissionDenied, StatusCodes.Status403Forbidden);
            }

            // Validate: GroupName must not exceed 255 characters
            if (request.GroupName.Length > 255)
            {
                throw new AppException(ErrorCodes.GroupNameInvalid);
            }

            // Validate: Description must not exceed 500 characters
            if (request.Description != null && request.Description.Length > 500)
            {
                throw new AppException(ErrorCodes.GroupDescriptionInvalid);
            }

            // Check if name is being changed
            if (group.GroupName != request.GroupName)
            {
                // Check if new name already exists in the same studio (excluding current group)
                var nameExists = await groupRepository.GroupNameExistsInStudioExcludingGroupAsync(
                    group.StudioId, request.GroupName, request.GroupId);

                if (nameExists)
                {
                    throw new AppException(ErrorCodes.GroupNameAlreadyExists);
                }

                group.GroupName = request.GroupName;
            }

            // Update description
            group.Description = request.Description;

            // validate and update personalization fields
            if (!string.IsNullOrEmpty(request.ColorHex))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(request.ColorHex, @"^#[0-9A-Fa-f]{6}$", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromMilliseconds(200)))
                {
                    throw new AppException(ErrorCodes.ValidationInvalidColor);
                }
                group.ColorHex = request.ColorHex;
            }
            else if (request.ColorHex == null)
            {
                // Allow null to reset the color
                group.ColorHex = null;
            }

            if (!string.IsNullOrEmpty(request.IconEmoji))
            {
                if (request.IconEmoji.Length > 10)
                {
                    throw new AppException(ErrorCodes.ValidationInvalidEmoji);
                }
                group.IconEmoji = request.IconEmoji;
            }
            else if (request.IconEmoji == null)
            {
                group.IconEmoji = null;
            }

            // validate and update BannerUrl
            if (!string.IsNullOrEmpty(request.BannerUrl) && !Uri.TryCreate(request.BannerUrl, UriKind.Absolute, out _))
            {
                throw new AppException(ErrorCodes.ValidationInvalidBannerUrl);
            }
            group.BannerUrl = request.BannerUrl;

            // Validate and update Tagline
            if (!string.IsNullOrEmpty(request.Tagline) && request.Tagline.Length > 200)
            {
                throw new AppException(ErrorCodes.ValidationStringLength);
            }
            group.Tagline = request.Tagline;

            // Validate and update Alias
            if (!string.IsNullOrEmpty(request.Alias))
            {
                if (request.Alias.Length > 50)
                {
                    throw new AppException(ErrorCodes.ValidationStringLength);
                }
                if (!System.Text.RegularExpressions.Regex.IsMatch(request.Alias, @"^[a-zA-Z0-9\sÀ-ỹ_\-]+$", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromMilliseconds(200)))
                {
                    throw new AppException(ErrorCodes.ValidationInvalidAlias);
                }
                group.Alias = request.Alias;
            }
            else if (request.Alias == null)
            {
                group.Alias = null;
            }

            // validate and update IsOpen setting (Owner/Moderator only)
            if (request.IsOpen.HasValue)
            {
                group.IsOpen = request.IsOpen.Value;
            }

            // validate and update AllowMemberUpdateProgress setting (Owner/Moderator only)
            if (request.AllowMemberUpdateProgress.HasValue)
            {
                group.AllowMemberUpdateProgress = request.AllowMemberUpdateProgress.Value;
            }

            // Handle template creation/deactivation
            var existingTemplate = await templateRepository.GetByGroupIdAsync(request.GroupId);
            Template? activeTemplate = null;

            if (request.IsTemplate && existingTemplate == null)
            {
                // Create new template
                var newTemplate = new Template
                {
                    TemplateId = Guid.NewGuid(),
                    UserId = userId,
                    GroupId = group.GroupId,
                    IsSystemTemplate = false, // User templates are not system templates
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                await templateRepository.AddAsync(newTemplate);
                activeTemplate = newTemplate;

                logger.LogInformation(
                    "Template created for group {GroupId} by user {UserId}. TemplateId: {TemplateId}",
                    group.GroupId, userId, newTemplate.TemplateId);
            }
            else if (!request.IsTemplate && existingTemplate != null && existingTemplate.IsActive)
            {
                // Deactivate existing template
                await templateRepository.DeleteAsync(existingTemplate);

                logger.LogInformation(
                    "Template deactivated for group {GroupId} by user {UserId}. TemplateId: {TemplateId}",
                    group.GroupId, userId, existingTemplate.TemplateId);
            }
            else if (request.IsTemplate && existingTemplate != null && !existingTemplate.IsActive)
            {
                // Reactivate existing template
                existingTemplate.IsActive = true;
                existingTemplate.UpdatedAt = DateTime.UtcNow;
                await templateRepository.UpdateAsync(existingTemplate);
                activeTemplate = existingTemplate;

                logger.LogInformation(
                    "Template reactivated for group {GroupId} by user {UserId}. TemplateId: {TemplateId}",
                    group.GroupId, userId, existingTemplate.TemplateId);
            }
            else if (request.IsTemplate && existingTemplate != null && existingTemplate.IsActive)
            {
                // Template already exists and is active
                activeTemplate = existingTemplate;
            }

            // Save changes
            await groupRepository.UpdateAsync(group);

            // Invalidate AI group cache so AI sees fresh group data immediately
            await cacheService.InvalidateAIGroupCacheAsync(group.GroupId);

            return new UpdateGroupResponse
            {
                GroupId = group.GroupId,
                GroupName = group.GroupName,
                Description = group.Description,
                StudioId = group.StudioId,
                GroupType = group.StudioId.HasValue ? "Studio" : "Independent",
                IsTemplate = activeTemplate != null,
                TemplateId = activeTemplate?.TemplateId,
                UpdatedAt = group.UpdatedAt,
                AvatarUrl = group.AvatarUrl,
                ColorHex = group.ColorHex,
                IconEmoji = group.IconEmoji,
                BannerUrl = group.BannerUrl,
                Tagline = group.Tagline,
                Alias = group.Alias,
                IsOpen = group.IsOpen,
                IsArchived = group.IsArchived,
                AllowMemberUpdateProgress = group.AllowMemberUpdateProgress
            };
        }

        public async Task<CreateStudioGroupsResponse> CreateStudioGroupAsync(Guid userId, CreateStudioGroupsRequest request)
        {
            int currentGroupCount = 0;

            if (request.StudioId.HasValue)
            {
                var studioId = request.StudioId.Value;

                var studio = await studioRepository.GetByIdAsync(studioId);
                if (studio == null)
                {
                    throw new AppException(ErrorCodes.StudioNotFound, StatusCodes.Status404NotFound);
                }

                var isOwner = await studioRepository.IsUserStudioOwnerAsync(studioId, userId);
                if (!isOwner)
                {
                    throw new AppException(ErrorCodes.StudioPermissionDenied, StatusCodes.Status403Forbidden);
                }
                currentGroupCount = await groupRepository.GetGroupCountByStudioIdAsync(request.StudioId.Value);

            }

            var subscriptionPlan = await userSubscriptionRepository.GetSubscriptionPlanByUserIdAsync(userId);
            var groupLimit = subscriptionPlan?.MaxGroups ?? 5;

            var createGroupCount = request.GroupCount;
            var totalGroupCount = currentGroupCount + createGroupCount;

            if (totalGroupCount > groupLimit)
            {
                throw new AppException(ErrorCodes.GroupLimitReached, StatusCodes.Status403Forbidden);
            }

            var existingNames = new HashSet<string>(
                await groupRepository.GetGroupNamesInStudioAsync(request.StudioId),
                StringComparer.OrdinalIgnoreCase);

            for (int i = currentGroupCount + 1; i <= totalGroupCount; i++)
            {
                var groupName = request.GroupPrefix + i;

                if (existingNames.Contains(groupName))
                {
                    throw new AppException(ErrorCodes.GroupNameAlreadyExists);
                }
            }

            List<GroupTaskStatus>? templateTaskStatuses = null;
            if (request.TemplateId.HasValue)
            {
                var template = await templateRepository.GetByIdAsync(request.TemplateId.Value);
                if (template == null)
                {
                    throw new AppException(ErrorCodes.TemplateNotFound, StatusCodes.Status404NotFound);
                }

                if (!template.IsSystemTemplate && template.UserId != userId)
                {
                    throw new AppException(ErrorCodes.TemplatePermissionDenied, StatusCodes.Status403Forbidden);
                }

                templateTaskStatuses = await groupTaskStatusRepository.GetByGroupIdAsync(template.GroupId);
            }

            var now = DateTime.UtcNow;
            var groupList = new List<Group>();

            for (int i = currentGroupCount + 1; i <= totalGroupCount; i++)
            {
                var groupName = request.GroupPrefix + i;
                var colorIndex = (i - currentGroupCount - 1) % BrandColors.Length;

                var newGroup = new Group
                {
                    GroupId = Guid.NewGuid(),
                    GroupName = groupName,
                    Description = request.Description,
                    StudioId = request.StudioId,
                    CreatedBy = userId,
                    IsTemplate = false,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    ColorHex = BrandColors[colorIndex]
                };

                await groupRepository.AddAsync(newGroup);

                var ownerParticipant = new GroupParticipant
                {
                    ParticipantId = Guid.NewGuid(),
                    GroupId = newGroup.GroupId,
                    UserId = userId,
                    Role = GroupRole.Owner,
                    CreatedAt = now
                };

                await groupParticipantRepository.AddAsync(ownerParticipant);

                if (templateTaskStatuses?.Count > 0)
                {
                    var newTaskStatuses = templateTaskStatuses.Select(ts => new GroupTaskStatus
                    {
                        StatusId = Guid.NewGuid(),
                        GroupId = newGroup.GroupId,
                        StatusName = ts.StatusName,
                        Position = ts.Position
                    }).ToList();

                    await groupTaskStatusRepository.AddRangeAsync(newTaskStatuses);
                }

                groupList.Add(newGroup);
            }

            return new CreateStudioGroupsResponse
            {
                CreateGroups = groupList.Select(g => new CreateGroupResponse
                {
                    GroupId = g.GroupId,
                    GroupName = g.GroupName,
                    Description = g.Description,
                    StudioId = g.StudioId,
                    GroupType = g.StudioId.HasValue ? "Studio" : "Independent",
                    CreatedBy = userId,
                    CreatedAt = g.CreatedAt
                }).ToList()
            };
        }

        public async Task<StudioGroupListResponse> GetStudioGroupsAsync(Guid userId, Guid studioId)
        {
            // Check if studio exists
            var studio = await studioRepository.GetByIdAsync(studioId);
            if (studio == null)
            {
                throw new AppException(ErrorCodes.StudioNotFound, StatusCodes.Status404NotFound);
            }

            // Check if user is owner or member of studio
            var userStudioParticipant = await studioParticipantRepository.GetByStudioAndUserAsync(studioId, userId);
            if (studio.OwnerId != userId && userStudioParticipant == null)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            // Get all groups belong to studio
            var groups = await groupRepository.GetStudioGroupsAsync(studioId);

            // If user is a member (not owner), filter to only groups they participate in
            List<Guid> allowedGroupIds;
            if (studio.OwnerId == userId)
            {
                // Owner can see all groups
                allowedGroupIds = groups.Select(g => g.GroupId).ToList();
            }
            else
            {
                // Member can only see groups they participate in
                var groupIds = groups.Select(g => g.GroupId).ToList();
                if (!groupIds.Any())
                {
                    allowedGroupIds = new List<Guid>();
                }
                else
                {
                    var userGroupParticipants = await groupParticipantRepository.GetByGroupIdsAsync(groupIds);
                    // 🔹 MODIFIED: Only show groups where user is an approved member
                    allowedGroupIds = userGroupParticipants
                        .Where(gp => gp.UserId == userId && gp.IsApproved)
                        .Select(gp => gp.GroupId)
                        .ToList();
                }
            }

            // Filter groups based on user's access
            var filteredGroups = groups.Where(g => allowedGroupIds.Contains(g.GroupId)).ToList();

            // Get all group IDs from filtered groups
            var groupIdsForQuery = filteredGroups.Select(g => g.GroupId).ToList();

            // Get all participants for member previews
            var allParticipants = await groupParticipantRepository.GetByGroupIdsAsync(groupIdsForQuery);

            var participantUserIds = allParticipants.Select(gp => gp.UserId).Distinct().ToList();
            var users = await userRepository.GetByIdsAsync(participantUserIds);

            // Get task counts
            var taskCounts = await taskRepository.GetTaskCountByGroupIdsAsync(groupIdsForQuery);

            var createdByUser = await userRepository.GetByIdAsync(userId);

            var groupCards = filteredGroups.Select(g =>
            {
                var groupParticipants = allParticipants
                    .Where(gp => gp.GroupId == g.GroupId)
                    .ToList();

                // Get user's actual role in this group
                var userParticipant = groupParticipants
                    .FirstOrDefault(gp => gp.GroupId == g.GroupId && gp.UserId == userId);
                var userRole = userParticipant?.Role ?? GroupRole.Owner;

                var membersPreview = groupParticipants
                    .Take(5)
                    .Select(gp =>
                    {
                        var user = users.FirstOrDefault(u => u.UserId == gp.UserId);
                        return new MemberPreviewDto
                        {
                            Id = gp.UserId,
                            FirstName = user?.FirstName ?? "",
                            LastName = user?.LastName ?? "",
                            AvatarUrl = AvatarUrlHelper.BuildAbsoluteAvatarUrl(user?.AvatarUrl, httpContextAccessor.HttpContext)
                        };
                    })
                    .ToList();

                return new GroupCardDto
                {
                    Id = g.GroupId,
                    Name = g.GroupName,
                    Description = g.Description ?? "",
                    IsFavorite = false,
                    Role = userRole.ToString(),
                    Studio = studio != null ? new StudioDto
                    {
                        Id = studioId,
                        Name = studio.StudioName
                    } : null,
                    CreatedBy = createdByUser != null ? new UserDto
                    {
                        Id = createdByUser.UserId,
                        FirstName = createdByUser.FirstName,
                        LastName = createdByUser.LastName,
                        AvatarUrl = AvatarUrlHelper.BuildAbsoluteAvatarUrl(createdByUser.AvatarUrl, httpContextAccessor.HttpContext)
                    } : new UserDto(),
                    MemberCount = groupParticipants.Count,
                    TaskCount = taskCounts.TryGetValue(g.GroupId, out var count) ? count : 0,
                    LastActivityAt = g.UpdatedAt,
                    MembersPreview = membersPreview,
                    AvatarUrl = g.AvatarUrl,
                    ColorHex = g.ColorHex,
                    IconEmoji = g.IconEmoji,
                    BannerUrl = g.BannerUrl,
                    Tagline = g.Tagline,
                    Alias = g.Alias
                };
            }).ToList();

            var studioGroups = groupCards.Where(g => g.Studio != null).ToList();

            var response = new StudioGroupListResponse
            {
                TotalGroup = filteredGroups.Count,
                StudioGroups = studioGroups,
            };

            return response;
        }

        /// <summary>
        /// Get paginated list of tasks in group with advanced filters
        /// Validate: User must be member of group
        /// Supports:
        /// - Search: Title, Description
        /// - Filter: Assignee, Status, Priority, Severity, StartDate range, DueDate range
        /// - Sort: createdAt, dueDate, startDate, priority, severity, progress (asc/desc)
        /// - Pagination: Database-level for optimal performance
        /// Returns: Task list + Group status list for filter dropdown
        /// </summary>
        public async Task<GroupTaskListResponse> GetGroupTasksAsync(
            Guid userId,
            Guid groupId,
            int page,
            int pageSize,
            string? search = null,
            Guid? assigneeId = null,
            Guid? statusId = null,
            TaskPriority? priority = null,
            TaskSeverity? severity = null,
            DateTime? startDateFrom = null,
            DateTime? startDateTo = null,
            DateTime? dueDateFrom = null,
            DateTime? dueDateTo = null,
            string? statusCategory = null,
            bool? hasNoAssignee = null,
            bool? hasNoDueDate = null,
            bool? overdue = null,
            string? sortBy = "createdAt",
            bool sortAscending = true)
        {
            // Validate group exists
            var group = await groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new AppException(ErrorCodes.GroupNotFound, StatusCodes.Status404NotFound);
            }

            // Validate user is an approved member of group
            var isUserInGroup = await groupParticipantRepository.IsUserApprovedInGroupAsync(groupId, userId);
            if (!isUserInGroup)
            {
                throw new AppException(ErrorCodes.GroupPermissionDenied, StatusCodes.Status403Forbidden);
            }

            // Validate page and pageSize
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 10 : pageSize;

            // Get group statuses for filter dropdown
            var groupStatuses = await groupTaskStatusRepository.GetByGroupIdAsync(groupId);
            var statusDtos = groupStatuses.Select(s => new TaskStatusInfoDto
            {
                StatusId = s.StatusId,
                StatusName = s.StatusName
            }).ToList();

            // Get tasks with filters and pagination from repository
            var (tasks, totalCount) = await taskRepository.GetGroupTasksWithFiltersAsync(
                groupId,
                page,
                pageSize,
                search,
                assigneeId,
                statusId,
                priority,
                severity,
                startDateFrom,
                startDateTo,
                dueDateFrom,
                dueDateTo,
                statusCategory,
                hasNoAssignee,
                hasNoDueDate,
                overdue,
                sortBy,
                sortAscending);

            // Get task IDs for loading assignees
            var taskIds = tasks.Select(t => t.TaskId).ToList();

            // Load assignees for all tasks
            Dictionary<Guid, List<UserDto>> taskAssigneesDict = new();
            if (taskIds.Any())
            {
                var assignments = await taskAssignmentRepository.GetListAssigneesByListTaskId(taskIds);
                var assigneeUserIds = assignments.Select(a => a.AssignedTo).Distinct().ToList();
                var assigneeUsers = await userRepository.GetByIdsAsync(assigneeUserIds);
                var userDict = assigneeUsers.ToDictionary(u => u.UserId);

                foreach (var assignment in assignments)
                {
                    if (userDict.TryGetValue(assignment.AssignedTo, out var user))
                    {
                        if (!taskAssigneesDict.ContainsKey(assignment.TaskId))
                        {
                            taskAssigneesDict[assignment.TaskId] = new List<UserDto>();
                        }

                        taskAssigneesDict[assignment.TaskId].Add(new UserDto
                        {
                            Id = user.UserId,
                            FirstName = user.FirstName,
                            LastName = user.LastName,
                            AvatarUrl = AvatarUrlHelper.BuildAbsoluteAvatarUrl(user.AvatarUrl, httpContextAccessor.HttpContext)
                        });
                    }
                }
            }

            // Map to response DTOs
            var taskItems = tasks.Select(t => new GroupTaskItemResponse
            {
                TaskId = t.TaskId,
                TaskTitle = t.Title,
                TaskDescription = t.Description,
                StatusName = t.GroupStatus?.StatusName ?? string.Empty,
                StatusId = t.GroupStatusId ?? Guid.Empty,
                TaskPriority = t.Priority,
                TaskSeverity = t.Severity,
                Progress = t.Progress,
                StartDate = t.StartDate,
                DueDate = t.DueDate,
                CompletedAt = t.CompletedAt,
                Position = t.Position,
                CreatedAt = t.CreatedAt,
                Assignees = taskAssigneesDict.TryGetValue(t.TaskId, out var assignees)
                    ? assignees
                    : new List<UserDto>(),
                CreatedBy = new UserDto
                {
                    Id = t.Owner.UserId,
                    FirstName = t.Owner.FirstName,
                    LastName = t.Owner.LastName,
                    AvatarUrl = AvatarUrlHelper.BuildAbsoluteAvatarUrl(t.Owner.AvatarUrl, httpContextAccessor.HttpContext)
                }
            }).ToList();

            // Calculate total pages
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

            return new GroupTaskListResponse
            {
                GroupId = groupId,
                GroupName = group.GroupName,
                Items = taskItems,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                GroupStatuses = statusDtos
            };
        }

        // Toggle IsOpen setting (Owner/Moderator only)
        public async Task<ToggleIsOpenResponse> ToggleIsOpenAsync(Guid userId, Guid groupId, bool isOpen)
        {
            var group = await groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new AppException(ErrorCodes.GroupNotFound, StatusCodes.Status404NotFound);
            }

            // Check if user is Owner or Moderator
            var userParticipant = await groupParticipantRepository.GetByGroupAndUserAsync(groupId, userId);
            if (userParticipant == null ||
                (userParticipant.Role != GroupRole.Owner && userParticipant.Role != GroupRole.Moderator))
            {
                throw new AppException(ErrorCodes.GroupUpdatePermissionDenied, StatusCodes.Status403Forbidden);
            }

            group.IsOpen = isOpen;
            group.UpdatedAt = DateTime.UtcNow;
            await groupRepository.UpdateAsync(group);

            logger.LogInformation(
                "User {UserId} toggled IsOpen to {IsOpen} for group {GroupId}",
                userId, isOpen, groupId);

            return new ToggleIsOpenResponse
            {
                Id = group.GroupId,
                Name = group.GroupName,
                IsOpen = group.IsOpen,
                UpdatedAt = group.UpdatedAt
            };
        }

        //  Get pending members (Owner/Moderator only)
        public async Task<PendingMemberListResponse> GetPendingMembersAsync(Guid userId, Guid groupId)
        {
            var group = await groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new AppException(ErrorCodes.GroupNotFound, StatusCodes.Status404NotFound);
            }

            // Check if user is Owner or Moderator
            var userParticipant = await groupParticipantRepository.GetByGroupAndUserAsync(groupId, userId);
            if (userParticipant == null ||
                (userParticipant.Role != GroupRole.Owner && userParticipant.Role != GroupRole.Moderator))
            {
                throw new AppException(ErrorCodes.GroupUpdatePermissionDenied, StatusCodes.Status403Forbidden);
            }

            // Get pending members
            var pendingParticipants = await groupParticipantRepository.GetPendingByGroupIdAsync(groupId);
            var userIds = pendingParticipants.Select(p => p.UserId).ToList();
            var users = await userRepository.GetByIdsAsync(userIds);

            var pendingMembers = pendingParticipants.Select(p =>
            {
                var user = users.FirstOrDefault(u => u.UserId == p.UserId);
                return new PendingMemberDto
                {
                    UserId = p.UserId,
                    FirstName = user?.FirstName ?? "",
                    LastName = user?.LastName ?? "",
                    Email = user?.Email ?? "",
                    AvatarUrl = AvatarUrlHelper.BuildAbsoluteAvatarUrl(user?.AvatarUrl, httpContextAccessor.HttpContext),
                    Role = p.Role.ToString(),
                    RequestedAt = p.CreatedAt
                };
            }).ToList();

            return new PendingMemberListResponse
            {
                GroupId = group.GroupId,
                GroupName = group.GroupName,
                TotalPending = pendingMembers.Count,
                PendingMembers = pendingMembers
            };
        }

        // Approve pending member (Owner/Moderator only)
        public async Task<ApproveMemberResponse> ApproveMemberAsync(Guid userId, Guid groupId, Guid targetUserId)
        {
            var group = await groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new AppException(ErrorCodes.GroupNotFound, StatusCodes.Status404NotFound);
            }

            // Check if user is Owner or Moderator
            var userParticipant = await groupParticipantRepository.GetByGroupAndUserAsync(groupId, userId);
            if (userParticipant == null ||
                (userParticipant.Role != GroupRole.Owner && userParticipant.Role != GroupRole.Moderator))
            {
                throw new AppException(ErrorCodes.GroupUpdatePermissionDenied, StatusCodes.Status403Forbidden);
            }

            // Get pending participant
            var targetParticipant = await groupParticipantRepository.GetPendingByGroupAndUserAsync(groupId, targetUserId);
            if (targetParticipant == null)
            {
                throw new AppException(ErrorCodes.GroupMemberNotFound, StatusCodes.Status404NotFound);
            }

            // Cannot approve Owner
            if (targetParticipant.Role == GroupRole.Owner)
            {
                throw new AppException(ErrorCodes.GroupMemberNotFound, StatusCodes.Status404NotFound);
            }

            // Cannot approve yourself
            if (targetUserId == userId)
            {
                throw new AppException(ErrorCodes.GroupMemberNotFound, StatusCodes.Status404NotFound);
            }

            // Check member limit
            var subscriptionPlan = await userSubscriptionRepository.GetSubscriptionPlanByUserIdAsync(userId);
            int memberLimit = subscriptionPlan?.MaxMembersPerGroup ?? 10;
            int currentMemberCount = await groupParticipantRepository.GetParticipantCountByGroupIdAsync(groupId);

            if (currentMemberCount >= memberLimit)
            {
                throw new AppException(ErrorCodes.GroupMemberLimitReached, StatusCodes.Status403Forbidden);
            }

            targetParticipant.IsApproved = true;
            await groupParticipantRepository.UpdateAsync(targetParticipant);

            // Invalidate AI member cache so AI sees fresh member data immediately
            try
            {
                await cacheService.InvalidateAIMemberCacheAsync(groupId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Non-critical AI member cache invalidation failed for GroupId={GroupId}",
                    groupId);
            }

            // Auto-approve user in studio if they were added as pending when joining group
            if (group.StudioId.HasValue)
            {
                var existingStudioParticipant = await studioParticipantRepository
                    .GetPendingByStudioAndUserAsync(group.StudioId.Value, targetUserId);

                if (existingStudioParticipant != null)
                {
                    // User was added as pending when joining the group - now approve them in studio
                    existingStudioParticipant.IsApproved = true;
                    await studioParticipantRepository.UpdateAsync(existingStudioParticipant);

                    logger.LogInformation(
                        "User {TargetUserId} auto-approved in studio {StudioId} after group {GroupId} approval",
                        targetUserId, group.StudioId.Value, groupId);
                }
                else
                {
                    // Check if user is already an approved studio member
                    var isStudioMember = await studioParticipantRepository
                        .IsUserApprovedInStudioAsync(group.StudioId.Value, targetUserId);

                    if (!isStudioMember)
                    {
                        // User not in studio at all - add them as approved member
                        var studioParticipant = new StudioParticipant
                        {
                            ParticipantId = Guid.NewGuid(),
                            StudioId = group.StudioId.Value,
                            UserId = targetUserId,
                            Role = StudioRole.Member,
                            IsApproved = true,
                            CreatedAt = DateTime.UtcNow
                        };

                        try
                        {
                            await studioParticipantRepository.AddAsync(studioParticipant);

                            logger.LogInformation(
                                "User {TargetUserId} auto-added to studio {StudioId} after group {GroupId} approval",
                                targetUserId, group.StudioId.Value, groupId);
                        }
                        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_StudioParticipants_StudioId_UserId") == true)
                        {
                            // User already in studio (race condition), ignore
                            logger.LogWarning(
                                "User {TargetUserId} already in studio {StudioId} when approving group {GroupId}",
                                targetUserId, group.StudioId.Value, groupId);
                        }
                    }
                }
            }

            var targetUser = await userRepository.GetByIdAsync(targetUserId);

            // Send approval notification email
            if (targetUser != null && !string.IsNullOrEmpty(targetUser.Email))
            {
                try
                {
                    var approvalUrl = BuildGroupUrl(groupId);
                    var language = targetUser?.Language == "vi" ? Language.Vietnamese : Language.English;
                    string nameToShow = targetUser?.Language == "vi" ? $"nhóm {group.GroupName}" : $"group {group.GroupName}";

                    var emailBody = EmailTemplate.MemberApprovedNotification(
                        nameToShow,
                        approvalUrl,
                        DateTime.UtcNow,
                        language);

                    await emailService.SendLinkAsync(
                        targetUser!.Email,
                        "Join group request approved",
                        emailBody);

                    logger.LogInformation(
                        "Approval notification email sent to {Email} for group {GroupId}",
                        targetUser.Email, groupId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Failed to send approval notification email to {Email}",
                        targetUser.Email);
                }
            }

            logger.LogInformation(
                "User {UserId} approved member {TargetUserId} in group {GroupId}",
                userId, targetUserId, groupId);

            return new ApproveMemberResponse
            {
                Id = group.GroupId,
                Name = group.GroupName,
                UserId = targetUserId,
                UserName = $"{targetUser?.FirstName} {targetUser?.LastName}",
                IsApproved = true,
                UpdatedAt = DateTime.UtcNow
            };
        }
        private string BuildGroupUrl(Guid groupId)
        {
            var baseUrl = configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
            return $"{baseUrl}/group/{groupId}";
        }

        public async Task<ArchiveGroupResponse> ToggleArchiveGroupAsync(Guid userId, Guid groupId, bool isArchived)
        {
            var group = await groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new AppException(ErrorCodes.GroupNotFound, StatusCodes.Status404NotFound);
            }

            // Only Owner can archive/unarchive
            var isOwner = await groupRepository.IsUserGroupOwnerAsync(groupId, userId);
            if (!isOwner)
            {
                throw new AppException(ErrorCodes.GroupPermissionDenied, StatusCodes.Status403Forbidden);
            }

            group.IsArchived = isArchived;
            group.UpdatedAt = DateTime.UtcNow;
            await groupRepository.UpdateAsync(group);

            logger.LogInformation(
                "User {UserId} set IsArchived to {IsArchived} for group {GroupId}",
                userId, isArchived, groupId);

            return new ArchiveGroupResponse
            {
                GroupId = group.GroupId,
                IsArchived = group.IsArchived,
                UpdatedAt = group.UpdatedAt
            };
        }
    }
}
