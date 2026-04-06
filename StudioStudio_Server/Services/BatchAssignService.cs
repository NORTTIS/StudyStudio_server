using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Localization;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.ExcelParsing;
using StudioStudio_Server.Services.Interfaces;
using StudioStudio_Server.Utils;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service for batch member assignment to groups within a studio
    /// </summary>
    public class BatchAssignService : IBatchAssignService
    {
        private readonly StudioDbContext _db;
        private readonly IGroupRepository _groupRepository;
        private readonly IGroupParticipantRepository _groupParticipantRepository;
        private readonly IStudioRepository _studioRepository;
        private readonly IStudioParticipantRepository _studioParticipantRepository;
        private readonly IUserSubscriptionRepository _userSubscriptionRepository;
        private readonly IExcelParser _excelParser;
        private readonly ILogger<BatchAssignService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IWebHostEnvironment _env;

        // Valid roles for batch assignment (Owner not allowed)
        private static readonly HashSet<string> ValidRoles = new(StringComparer.OrdinalIgnoreCase)
        {
            "Member", "Moderator", "Commenter", "Viewer"
        };

        public BatchAssignService(
            StudioDbContext db,
            IGroupRepository groupRepository,
            IGroupParticipantRepository groupParticipantRepository,
            IStudioRepository studioRepository,
            IStudioParticipantRepository studioParticipantRepository,
            IUserSubscriptionRepository userSubscriptionRepository,
            IExcelParser excelParser,
            ILogger<BatchAssignService> logger,
            IHttpContextAccessor httpContextAccessor,
            IWebHostEnvironment env)
        {
            _db = db;
            _groupRepository = groupRepository;
            _groupParticipantRepository = groupParticipantRepository;
            _studioRepository = studioRepository;
            _studioParticipantRepository = studioParticipantRepository;
            _userSubscriptionRepository = userSubscriptionRepository;
            _excelParser = excelParser;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _env = env;
        }

        /// <summary>
        /// Create a new group when group name is not found in batch assign
        /// Validates: plan limit, duplicate name within studio
        /// </summary>
        private async Task<Group> CreateMissingGroupAsync(
            Guid studioId,
            string groupName,
            Guid userId,
            CancellationToken cancellationToken)
        {
            // 1. Check plan limit
            var plan = await _userSubscriptionRepository.GetSubscriptionPlanByUserIdAsync(userId);
            var groupLimit = plan?.MaxGroups ?? 5;
            var currentCount = await _groupRepository.CountGroupsCreatedByUserAsync(userId);

            if (currentCount >= groupLimit)
                throw new AppException(ErrorCodes.GroupLimitReached, StatusCodes.Status403Forbidden);

            // 2. Check duplicate name in studio
            var nameExists = await _groupRepository.GroupNameExistsInStudioAsync(studioId, groupName, userId);
            if (nameExists)
                throw new AppException(ErrorCodes.GroupNameAlreadyExists, StatusCodes.Status400BadRequest);

            // 3. Create group
            var newGroup = new Group
            {
                GroupId = Guid.NewGuid(),
                GroupName = groupName,
                StudioId = studioId,
                CreatedBy = userId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _groupRepository.AddAsync(newGroup);

            // 4. Add creator as Owner
            var ownerParticipant = new GroupParticipant
            {
                ParticipantId = Guid.NewGuid(),
                GroupId = newGroup.GroupId,
                UserId = userId,
                Role = GroupRole.Owner,
                CreatedAt = DateTime.UtcNow
            };
            await _groupParticipantRepository.AddAsync(ownerParticipant);

            return newGroup;
        }

        /// <summary>
        /// Get localized message for error code
        /// </summary>
        private string GetLocalizedMessage(string errorCode)
        {
            var culture = HttpContextHelper.GetCultureFromHeader(_httpContextAccessor.HttpContext!);
            var localizer = new JsonStringLocalizer(_env, culture);
            return localizer.Get(errorCode);
        }

        /// <summary>
        /// Create a BatchErrorRow with localized message
        /// </summary>
        private BatchErrorRow CreateErrorRow(int rowNumber, string? email, string? groupName, string reasonCode)
        {
            return new BatchErrorRow
            {
                Row = rowNumber,
                Email = email,
                GroupName = groupName,
                Reason = reasonCode,
                Message = GetLocalizedMessage(reasonCode)
            };
        }

        /// <summary>
        /// Process batch assignment from CSV/Excel file
        /// Validates all rows first, then commits in a single transaction
        /// </summary>
        public async Task<BatchAssignResponse> BatchAssignAsync(
            Guid studioId,
            Guid userId,
            Stream stream,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            // 1. Validate studio exists
            var studio = await _studioRepository.GetByIdAsync(studioId);
            if (studio == null)
            {
                throw new AppException(ErrorCodes.BatchStudioNotFound, StatusCodes.Status404NotFound);
            }

            if (studio.IsArchived)
            {
                throw new AppException(ErrorCodes.StudioIsArchived, StatusCodes.Status403Forbidden);
            }

            // 2. Validate user is studio owner
            if (studio.OwnerId != userId)
            {
                throw new AppException(ErrorCodes.BatchNotStudioOwner, StatusCodes.Status403Forbidden);
            }

            // 3. Get plan limits
            var plan = await _userSubscriptionRepository.GetSubscriptionPlanByUserIdAsync(userId);
            var maxMembersPerGroup = plan?.MaxMembersPerGroup ?? 10;

            // 4. Get studio groups (can be empty if creating new groups via CSV)
            var studioGroups = await _groupRepository.GetStudioGroupsAsync(studioId);

            // 5. Get studio members with email lookup
            var studioParticipants = await _studioParticipantRepository.GetParticipantsByStudioIdAsync(studioId);
            var emailToUserId = studioParticipants
                .Where(sp => sp.User != null)
                .ToDictionary(sp => sp.User!.Email.ToLowerInvariant(), sp => sp.UserId);

            // 6. Parse the file
            var parseResult = await _excelParser.ParseAsync(stream, fileName, cancellationToken);
            if (parseResult.ErrorCode != null)
            {
                throw new AppException(parseResult.ErrorCode, StatusCodes.Status400BadRequest);
            }

            if (parseResult.Rows.Count == 0)
            {
                return new BatchAssignResponse
                {
                    TotalRows = 0,
                    SuccessCount = 0,
                    SkippedCount = 0,
                    Errors = new List<BatchErrorRow>(),
                    Assignments = new List<BatchAssignmentItem>()
                };
            }

            // 7. Validate and collect all operations
            var groupNameToGroup = studioGroups.ToDictionary(g => g.GroupName.ToLowerInvariant(), g => g);
            var groupIds = studioGroups.Select(g => g.GroupId).ToList();

            // Get existing group participants
            var existingParticipants = await _groupParticipantRepository.GetByGroupIdsAsync(groupIds);
            var existingByGroupAndUser = existingParticipants
                .ToDictionary(p => (p.GroupId, p.UserId), p => p);

            // Track seen email+group combinations in file
            var seenCombinations = new HashSet<(string email, string group)>();
            var pendingAdds = new List<(Guid userId, Guid groupId, GroupRole role, ParsedBatchRow row)>();
            var pendingUpdates = new List<(GroupParticipant existing, GroupRole newRole, ParsedBatchRow row)>();
            var assignments = new List<BatchAssignmentItem>();
            var errors = new List<BatchErrorRow>();

            // Count current members per group
            var currentMemberCounts = existingParticipants
                .GroupBy(p => p.GroupId)
                .ToDictionary(g => g.Key, g => g.Count());

            // Process each row
            foreach (var row in parseResult.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Check for duplicate email+group in file
                var combo = (row.Email.ToLowerInvariant(), row.GroupName.ToLowerInvariant());
                if (!seenCombinations.Add(combo))
                {
                    errors.Add(CreateErrorRow(row.RowNumber, row.Email, row.GroupName, ErrorCodes.BatchDuplicateEmailGroupInFile));
                    continue;
                }

                // Validate email format
                if (string.IsNullOrWhiteSpace(row.Email) || !IsValidEmail(row.Email))
                {
                    errors.Add(CreateErrorRow(row.RowNumber, row.Email, row.GroupName, ErrorCodes.ValidationInvalidEmailFormat));
                    continue;
                }

                // Validate group name
                if (string.IsNullOrWhiteSpace(row.GroupName))
                {
                    errors.Add(CreateErrorRow(row.RowNumber, row.Email, row.GroupName, ErrorCodes.ValidationRequiredField));
                    continue;
                }

                if (!groupNameToGroup.TryGetValue(row.GroupName.ToLowerInvariant(), out var targetGroup))
                {
                    // Group doesn't exist → create new
                    try
                    {
                        targetGroup = await CreateMissingGroupAsync(studioId, row.GroupName, userId, cancellationToken);
                        groupNameToGroup[row.GroupName.ToLowerInvariant()] = targetGroup;
                        groupIds.Add(targetGroup.GroupId);
                        currentMemberCounts[targetGroup.GroupId] = 0;
                    }
                    catch (AppException ex)
                    {
                        // Plan limit reached or duplicate name
                        errors.Add(CreateErrorRow(row.RowNumber, row.Email, row.GroupName, ex.Code));
                        continue;
                    }
                }

                // Validate role
                if (string.IsNullOrWhiteSpace(row.Role))
                {
                    errors.Add(CreateErrorRow(row.RowNumber, row.Email, row.GroupName, ErrorCodes.ValidationRequiredField));
                    continue;
                }

                if (row.Role.Equals("Owner", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(CreateErrorRow(row.RowNumber, row.Email, row.GroupName, ErrorCodes.BatchCannotAssignOwnerRole));
                    continue;
                }

                if (!ValidRoles.Contains(row.Role))
                {
                    errors.Add(CreateErrorRow(row.RowNumber, row.Email, row.GroupName, ErrorCodes.ValidationInvalidRoleValue));
                    continue;
                }

                // Look up user by email
                if (!emailToUserId.TryGetValue(row.Email.ToLowerInvariant(), out var targetUserId))
                {
                    errors.Add(CreateErrorRow(row.RowNumber, row.Email, row.GroupName, ErrorCodes.ValidationUserNotInStudio));
                    continue;
                }

                // Check if user is already in ANY OTHER group in the studio (one member per group rule)
                // Only for new assignments (not existing participants in the target group)
                var userInOtherGroup = existingByGroupAndUser.Keys
                    .Any(k => k.UserId == targetUserId && k.GroupId != targetGroup.GroupId);

                if (userInOtherGroup)
                {
                    errors.Add(CreateErrorRow(row.RowNumber, row.Email, row.GroupName, ErrorCodes.BatchMemberAlreadyInAnotherGroup));
                    continue;
                }

                // Parse role
                if (!Enum.TryParse<GroupRole>(row.Role, ignoreCase: true, out var targetRole))
                {
                    errors.Add(CreateErrorRow(row.RowNumber, row.Email, row.GroupName, ErrorCodes.ValidationInvalidRoleValue));
                    continue;
                }

                // Check if user is already in group
                if (existingByGroupAndUser.TryGetValue((targetGroup.GroupId, targetUserId), out var existingParticipant))
                {
                    // User is already a member - check if role is same
                    if (existingParticipant.Role == targetRole)
                    {
                        // Same role - skip
                        assignments.Add(new BatchAssignmentItem
                        {
                            Email = row.Email,
                            GroupName = row.GroupName,
                            Role = row.Role,
                            Action = "Skipped"
                        });
                        continue;
                    }

                    if (existingParticipant.Role == GroupRole.Owner)
                    {
                        // Cannot change owner's role - skip
                        assignments.Add(new BatchAssignmentItem
                        {
                            Email = row.Email,
                            GroupName = row.GroupName,
                            Role = row.Role,
                            Action = "Skipped"
                        });
                        continue;
                    }

                    // Role is different - update
                    pendingUpdates.Add((existingParticipant, targetRole, row));
                }
                else
                {
                    // New member - check limit
                    var currentCount = currentMemberCounts.GetValueOrDefault(targetGroup.GroupId, 0);
                    if (currentCount >= maxMembersPerGroup)
                    {
                        errors.Add(CreateErrorRow(row.RowNumber, row.Email, row.GroupName, ErrorCodes.BatchGroupMemberLimitExceeded));
                        continue;
                    }

                    // Check moderator conflict
                    if (targetRole == GroupRole.Moderator)
                    {
                        var currentModeratorCount = existingParticipants
                            .Count(p => p.GroupId == targetGroup.GroupId && p.Role == GroupRole.Moderator);

                        if (currentModeratorCount > 0)
                        {
                            errors.Add(CreateErrorRow(row.RowNumber, row.Email, row.GroupName, ErrorCodes.BatchGroupAlreadyHasModerator));
                            continue;
                        }

                        // Also check pending adds for this group
                        if (pendingAdds.Any(p => p.groupId == targetGroup.GroupId && p.role == GroupRole.Moderator))
                        {
                            errors.Add(CreateErrorRow(row.RowNumber, row.Email, row.GroupName, ErrorCodes.BatchGroupAlreadyHasModerator));
                            continue;
                        }
                    }

                    pendingAdds.Add((targetUserId, targetGroup.GroupId, targetRole, row));
                }
            }

            // 8. Execute all changes in a single transaction
            using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Add new participants
                foreach (var (targetUserId, groupId, role, row) in pendingAdds)
                {
                    var newParticipant = new GroupParticipant
                    {
                        ParticipantId = Guid.NewGuid(),
                        GroupId = groupId,
                        UserId = targetUserId,
                        Role = role,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.GroupParticipants.Add(newParticipant);

                    // Update count
                    currentMemberCounts[groupId] = currentMemberCounts.GetValueOrDefault(groupId, 0) + 1;

                    assignments.Add(new BatchAssignmentItem
                    {
                        Email = row.Email,
                        GroupName = row.GroupName,
                        Role = row.Role,
                        Action = "Added"
                    });
                }

                // Update existing participants
                foreach (var (existing, newRole, row) in pendingUpdates)
                {
                    existing.Role = newRole;
                    _db.GroupParticipants.Update(existing);

                    assignments.Add(new BatchAssignmentItem
                    {
                        Email = row.Email,
                        GroupName = row.GroupName,
                        Role = row.Role,
                        Action = "RoleUpdated"
                    });
                }

                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch assign transaction failed");
                await transaction.RollbackAsync(cancellationToken);
                throw new AppException(ErrorCodes.UnexpectedError, StatusCodes.Status500InternalServerError);
            }

            return new BatchAssignResponse
            {
                TotalRows = parseResult.TotalRows,
                SuccessCount = assignments.Count,
                SkippedCount = assignments.Count(a => a.Action == "Skipped"),
                Errors = errors,
                Assignments = assignments
            };
        }

        /// <summary>
        /// Randomly assign studio members to groups
        /// Key rules:
        /// - Studio owner is excluded from assignment pool
        /// - Each member is assigned to only ONE group (one member per group)
        /// </summary>
        public async Task<RandomAssignResponse> RandomAssignAsync(
            Guid studioId,
            Guid userId,
            RandomAssignRequest request,
            CancellationToken cancellationToken = default)
        {
            // 1. Validate studio exists
            var studio = await _studioRepository.GetByIdAsync(studioId);
            if (studio == null)
            {
                throw new AppException(ErrorCodes.BatchStudioNotFound, StatusCodes.Status404NotFound);
            }

            if (studio.IsArchived)
            {
                throw new AppException(ErrorCodes.StudioIsArchived, StatusCodes.Status403Forbidden);
            }

            // 2. Validate user is studio owner
            if (studio.OwnerId != userId)
            {
                throw new AppException(ErrorCodes.BatchNotStudioOwner, StatusCodes.Status403Forbidden);
            }

            // 3. Validate role is not Owner
            if (request.DefaultRole == GroupRole.Owner)
            {
                throw new AppException(ErrorCodes.BatchCannotAssignOwnerRole, StatusCodes.Status400BadRequest);
            }

            // 4. Get plan limits
            var plan = await _userSubscriptionRepository.GetSubscriptionPlanByUserIdAsync(userId);
            var maxMembersPerGroup = plan?.MaxMembersPerGroup ?? 10;

            // 5. Get studio groups
            List<Group> targetGroups;
            if (request.TargetGroupIds != null && request.TargetGroupIds.Count > 0)
            {
                targetGroups = await _groupRepository.GetByIdsAsync(request.TargetGroupIds);
                // Filter to only groups that belong to this studio
                targetGroups = targetGroups.Where(g => g.StudioId == studioId).ToList();
            }
            else
            {
                targetGroups = await _groupRepository.GetStudioGroupsAsync(studioId);
            }

            if (targetGroups.Count == 0)
            {
                throw new AppException(ErrorCodes.BatchNoGroupsInStudio, StatusCodes.Status400BadRequest);
            }

            // 6. Get existing participants for target groups (approved only)
            var groupIds = targetGroups.Select(g => g.GroupId).ToList();
            var existingParticipants = await _groupParticipantRepository.GetByGroupIdsAsync(groupIds);
            var existingByGroupAndUser = existingParticipants
                .ToDictionary(p => (p.GroupId, p.UserId), p => p);

            // 6b. Also load pending (unapproved) records for these groups
            // These will be removed when users are assigned to the same group
            var pendingParticipants = await _groupParticipantRepository.GetPendingByGroupIdsAsync(groupIds);

            // 7. Check moderator conflicts BEFORE any DB write
            var conflicts = new List<GroupConflictInfo>();
            if (request.DefaultRole == GroupRole.Moderator)
            {
                foreach (var group in targetGroups)
                {
                    var hasModerator = existingParticipants.Any(p => p.GroupId == group.GroupId && p.Role == GroupRole.Moderator);
                    if (hasModerator)
                    {
                        conflicts.Add(new GroupConflictInfo
                        {
                            GroupId = group.GroupId,
                            GroupName = group.GroupName,
                            Reason = ErrorCodes.BatchGroupAlreadyHasModerator
                        });
                    }
                }

                if (conflicts.Count > 0)
                {
                    return new RandomAssignResponse
                    {
                        Success = false,
                        AssignedCount = 0,
                        Groups = new List<GroupAssignmentSummary>(),
                        Conflicts = conflicts
                    };
                }
            }

            // 8. Get studio members
            var studioParticipants = await _studioParticipantRepository.GetParticipantsByStudioIdAsync(studioId);

            // Exclude users and studio owner
            var excludeUserIds = request.ExcludeUserIds ?? new List<Guid>();
            var memberUserIds = studioParticipants
                .Where(sp => sp.UserId != studio.OwnerId) // Exclude studio owner
                .Where(sp => !excludeUserIds.Contains(sp.UserId))
                .Select(sp => sp.UserId)
                .ToList();

            // 9. Determine which members to assign based on scope
            List<Guid> membersToAssign;
            if (request.Scope == AssignScope.Unassigned)
            {
                // Only assign members not already in any target group
                membersToAssign = memberUserIds
                    .Where(mId => !existingByGroupAndUser.Keys.Any(k => k.UserId == mId && groupIds.Contains(k.GroupId)))
                    .ToList();
            }
            else // All
            {
                membersToAssign = memberUserIds;
            }

            // 10. If scope is All, remove non-owner members first before reassigning
            if (request.Scope == AssignScope.All)
            {
                var nonOwnerParticipants = existingParticipants
                    .Where(p => p.Role != GroupRole.Owner)
                    .ToList();

                if (nonOwnerParticipants.Count > 0)
                {
                    await _groupParticipantRepository.RemoveRangeAsync(nonOwnerParticipants);

                    // Update local state
                    foreach (var p in nonOwnerParticipants)
                    {
                        existingByGroupAndUser.Remove((p.GroupId, p.UserId));
                    }
                }
            }

            // 11. Calculate current member counts per group
            var currentMemberCounts = targetGroups.ToDictionary(g => g.GroupId, g =>
                existingParticipants.Count(p => p.GroupId == g.GroupId && p.Role != GroupRole.Owner));

            // 12. Assign members using Pure Random strategy
            // Each member is assigned to only ONE group with available slots
            var groupList = targetGroups.ToList();

            var newParticipants = new List<GroupParticipant>();
            var groupSummaries = new Dictionary<Guid, GroupAssignmentSummary>();

            foreach (var group in targetGroups)
            {
                groupSummaries[group.GroupId] = new GroupAssignmentSummary
                {
                    GroupId = group.GroupId,
                    GroupName = group.GroupName,
                    MemberCount = 0,
                    Members = new List<MemberAssignmentDetail>()
                };
            }

            // Pure random assignment: each member is assigned to a random group with available slots
            var rng = new Random();
            foreach (var memberUserId in membersToAssign)
            {
                // Filter groups with available slots
                var availableGroups = groupList
                    .Where(g => currentMemberCounts[g.GroupId] < maxMembersPerGroup)
                    .ToList();

                if (availableGroups.Count == 0) break;

                // Select a random group
                var group = availableGroups[rng.Next(availableGroups.Count)];

                // Create participant
                var newParticipant = new GroupParticipant
                {
                    ParticipantId = Guid.NewGuid(),
                    GroupId = group.GroupId,
                    UserId = memberUserId,
                    Role = request.DefaultRole,
                    CreatedAt = DateTime.UtcNow
                };
                newParticipants.Add(newParticipant);
                currentMemberCounts[group.GroupId]++;

                // Add to summary
                var userEmail = studioParticipants.FirstOrDefault(sp => sp.UserId == memberUserId)?.User?.Email ?? memberUserId.ToString();
                groupSummaries[group.GroupId].Members.Add(new MemberAssignmentDetail
                {
                    UserId = memberUserId,
                    Email = userEmail,
                    Role = request.DefaultRole.ToString()
                });
                groupSummaries[group.GroupId].MemberCount++;
            }

            // 13. Remove pending approval records BEFORE assigning members
            // Only remove pending requests for users who are actually being assigned
            var usersBeingAssigned = newParticipants.Select(np => np.UserId).ToHashSet();
            var pendingToRemove = pendingParticipants
                .Where(p => usersBeingAssigned.Contains(p.UserId))
                .ToList();

            if (pendingToRemove.Count > 0)
            {
                await _groupParticipantRepository.RemoveRangeAsync(pendingToRemove);
            }

            // 14. Save to database
            if (newParticipants.Count > 0)
            {
                await _groupParticipantRepository.AddRangeAsync(newParticipants);
            }

            return new RandomAssignResponse
            {
                Success = true,
                AssignedCount = newParticipants.Count,
                Groups = groupSummaries.Values.Where(g => g.MemberCount > 0).ToList(),
                Conflicts = null
            };
        }

        /// <summary>
        /// Generate pre-filled CSV template for batch assignment
        /// Key rules:
        /// - Studio owner is excluded from template
        /// - Each member appears in only ONE row with ONE group (round-robin)
        /// </summary>
        public async Task<byte[]> GenerateTemplateAsync(Guid studioId, Guid userId)
        {
            // Validate studio exists
            var studio = await _studioRepository.GetByIdAsync(studioId);
            if (studio == null)
            {
                throw new AppException(ErrorCodes.BatchStudioNotFound, StatusCodes.Status404NotFound);
            }

            if (studio.IsArchived)
            {
                throw new AppException(ErrorCodes.StudioIsArchived, StatusCodes.Status403Forbidden);
            }

            // Validate user is studio owner
            if (studio.OwnerId != userId)
            {
                throw new AppException(ErrorCodes.BatchNotStudioOwner, StatusCodes.Status403Forbidden);
            }

            // Get studio members and groups
            var studioParticipants = await _studioParticipantRepository.GetParticipantsByStudioIdAsync(studioId);
            var studioGroups = await _groupRepository.GetStudioGroupsAsync(studioId);

            var sb = new StringBuilder();

            // Header
            sb.AppendLine("Email,GroupName,Role");

            // Exclude studio owner from participants
            var eligibleParticipants = studioParticipants
                .Where(sp => sp.UserId != studio.OwnerId && sp.User != null)
                .ToList();

            if (eligibleParticipants.Count == 0 || studioGroups.Count == 0)
            {
                // Return empty template with just header
                return new UTF8Encoding(true).GetBytes(sb.ToString());
            }

            // Round-robin assignment: each member gets one group
            var groupIndex = 0;
            foreach (var participant in eligibleParticipants)
            {
                if (participant.User == null) continue;

                var group = studioGroups[groupIndex % studioGroups.Count];
                sb.AppendLine($"{participant.User.Email},{group.GroupName},Member");
                groupIndex++;
            }

            // UTF-8 BOM for Excel compatibility with Vietnamese
            return new UTF8Encoding(true).GetBytes(sb.ToString());
        }

        /// <summary>
        /// Validate email format
        /// </summary>
        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}
