using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository xử lý các thao tác CRUD với Group entity
    /// </summary>
    public class GroupRepository : IGroupRepository
    {
        private readonly StudioDbContext _db;

        public GroupRepository(StudioDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Get list of groups user is member of
        /// Condition: Participants contains {userId} AND IsActive = true
        /// Include: Studio, Participants
        /// Order by: UpdatedAt DESC
        /// </summary>
        public async Task<List<Group>> GetUserGroupsAsync(Guid userId)
        {
            return await _db.Groups
                .Where(g => g.Participants.Any(p => p.UserId == userId) && g.IsActive)
                .Include(g => g.Participants)
                .OrderByDescending(g => g.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Get group by ID
        /// Condition: GroupId = {groupId} AND IsActive = true
        /// No Include
        /// </summary>
        public async Task<Group?> GetByIdAsync(Guid groupId)
        {
            return await _db.Groups
                .Include(g => g.Participants)
                .FirstOrDefaultAsync(g => g.GroupId == groupId && g.IsActive);
        }

        /// <summary>
        /// Get group with details
        /// Condition: GroupId = {groupId} AND IsActive = true
        /// Include: Studio, Participants → User
        /// </summary>
        public async Task<Group?> GetGroupWithDetailsAsync(Guid groupId)
        {
            return await _db.Groups
                .Include(g => g.Participants)
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.GroupId == groupId && g.IsActive);
        }

        /// <summary>
        /// Get list of groups in studio
        /// Condition: StudioId = {studioId} AND IsActive = true
        /// Order by: GroupName DESC, then CreatedAt DESC
        /// </summary>
        public async Task<List<Group>> GetStudioGroupsAsync(Guid studioId)
        {
            return await _db.Groups
                .Where(g => g.StudioId == studioId && g.IsActive)
                .OrderByDescending(g => g.GroupName)
                .ThenByDescending(g => g.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Get groups by list of IDs
        /// Condition: GroupId IN {groupIds} AND IsActive = true
        /// </summary>
        public async Task<List<Group>> GetByIdsAsync(List<Guid> groupIds)
        {
            return await _db.Groups
                .Where(g => groupIds.Contains(g.GroupId) && g.IsActive)
                .ToListAsync();
        }

        /// <summary>
        /// Count groups created by user (Owner)
        /// Condition: Participants contains {userId with Role = Owner} AND IsActive = true
        /// Use case: Check group creation limit
        /// </summary>
        public async Task<int> CountGroupsCreatedByUserAsync(Guid userId)
        {
            return await _db.Groups
                .Where(g => g.Participants.Any(p => p.UserId == userId && p.Role == GroupRole.Owner) && g.IsActive)
                .CountAsync();
        }

        /// <summary>
        /// Count groups in studio
        /// Condition: StudioId = {studioId} AND IsActive = true
        /// </summary>
        public async Task<int> GetGroupCountByStudioIdAsync(Guid studioId)
        {
            return await _db.Groups
                .Where(g => g.StudioId == studioId && g.IsActive)
                .CountAsync();
        }

        /// <summary>
        /// Check if group name exists
        /// - If studioId != null: check within studio
        /// - If studioId == null: check within user's personal groups
        /// </summary>
        public async Task<bool> GroupNameExistsInStudioAsync(Guid? studioId, string groupName, Guid? userId)
        {
            return await _db.Groups
                .AnyAsync(g =>
                    g.GroupName == groupName &&
                    g.IsActive &&
                    (
                        (studioId != null && g.StudioId == studioId) ||
                        (studioId == null && g.StudioId == null && g.CreatedBy == userId)
                    )
                );
        }

        /// <summary>
        /// Check if group name exists in studio (excluding group being updated)
        /// Condition: StudioId = {studioId} AND GroupName = {groupName} AND GroupId != {excludeGroupId} AND IsActive = true
        /// Use case: Validate when updating group name
        /// </summary>
        public async Task<bool> GroupNameExistsInStudioExcludingGroupAsync(
            Guid? studioId,
            string groupName,
            Guid excludeGroupId)
        {
            return await _db.Groups
                .AnyAsync(g => g.StudioId == studioId &&
                              g.GroupName == groupName &&
                              g.GroupId != excludeGroupId &&
                              g.IsActive);
        }

        /// <summary>
        /// Check if user is Owner of group
        /// Condition: GroupId = {groupId} AND IsActive = true AND Participants contains {userId with Role = Owner}
        /// </summary>
        public async Task<bool> IsUserGroupOwnerAsync(Guid groupId, Guid userId)
        {
            return await _db.Groups
                .Where(g => g.GroupId == groupId && g.IsActive)
                .AnyAsync(g => g.Participants.Any(p => p.UserId == userId && p.Role == GroupRole.Owner));
        }

        /// <summary>
        /// Add new group to database
        /// </summary>
        public async Task AddAsync(Group group)
        {
            _db.Groups.Add(group);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Update group information
        /// Auto-set: UpdatedAt = UtcNow
        /// </summary>
        public async Task UpdateAsync(Group group)
        {
            group.UpdatedAt = DateTime.UtcNow;
            _db.Groups.Update(group);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Soft delete group
        /// Set IsActive = false, UpdatedAt = UtcNow
        /// Note: Participants, tasks, messages remain in database
        /// </summary>
        public async Task DeleteAsync(Group group)
        {
            group.IsActive = false;
            group.UpdatedAt = DateTime.UtcNow;
            _db.Groups.Update(group);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Get group owner ID
        /// Condition: GroupId = {groupId} AND IsActive = true
        /// Returns: UserId of Owner
        /// </summary>
        public async Task<Guid> GetGroupOwnerIdAsync(Guid groupId)
        {
            var owner = await _db.GroupParticipants
                .Where(p => p.GroupId == groupId && p.Role == GroupRole.Owner)
                .Select(p => p.UserId)
                .FirstOrDefaultAsync();

            return owner;
        }

        /// <summary>
        /// Get list of group names in studio
        /// Condition: StudioId = {studioId} AND IsActive = true
        /// Returns: List of group names
        /// </summary>
        public async Task<List<string>> GetGroupNamesInStudioAsync(Guid? studioId)
        {
            return await _db.Groups
                .Where(g => g.StudioId == studioId && g.IsActive)
                .Select(g => g.GroupName)
                .ToListAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Get paginated groups with filters for admin
        /// </summary>
        public async Task<(List<Group> Groups, int TotalCount)> GetGroupsAsync(
            string? searchTerm,
            string? groupType,
            int pageNumber,
            int pageSize)
        {
            var query = _db.Groups.AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(g => g.GroupName.Contains(searchTerm));
            }

            // Apply group type filter
            if (!string.IsNullOrWhiteSpace(groupType))
            {
                if (groupType.Equals("Studio", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(g => g.StudioId != null);
                }
                else if (groupType.Equals("Independent", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(g => g.StudioId == null);
                }
            }

            // Get total count before pagination
            var totalCount = await query.CountAsync();

            // Apply pagination and ordering
            var groups = await query
                .OrderByDescending(g => g.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();

            return (groups, totalCount);
        }

        /// <summary>
        /// Get member counts for a list of groups
        /// </summary>
        public async Task<Dictionary<Guid, int>> GetMemberCountsAsync(List<Guid> groupIds)
        {
            if (groupIds == null || groupIds.Count == 0)
            {
                return new Dictionary<Guid, int>();
            }

            var counts = await _db.GroupParticipants
                .Where(p => groupIds.Contains(p.GroupId))
                .GroupBy(p => p.GroupId)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.GroupId, x => x.Count);

            // Ensure all groupIds have an entry (default to 0 for groups with no members)
            return groupIds.ToDictionary(g => g, g => counts.ContainsKey(g) ? counts[g] : 0);
        }

        /// <summary>
        /// Get task counts for a list of groups
        /// </summary>
        public async Task<Dictionary<Guid, int>> GetTaskCountsAsync(List<Guid> groupIds)
        {
            if (groupIds == null || groupIds.Count == 0)
            {
                return new Dictionary<Guid, int>();
            }

            var counts = await _db.Tasks
                .Where(t => groupIds.Contains(t.GroupId!.Value))
                .GroupBy(t => t.GroupId)
                .Select(g => new { GroupId = g.Key!.Value, Count = g.Count() })
                .ToDictionaryAsync(x => x.GroupId, x => x.Count);

            return groupIds.ToDictionary(g => g, g => counts.ContainsKey(g) ? counts[g] : 0);
        }

        /// <summary>
        /// Get last activity for a list of groups
        /// Last activity = MAX(Group.UpdatedAt, MAX(TaskItem.UpdatedAt), MAX(GroupMessage.CreatedAt))
        /// </summary>
        public async Task<Dictionary<Guid, DateTime?>> GetLastActivityAsync(List<Guid> groupIds)
        {
            if (groupIds == null || groupIds.Count == 0)
            {
                return new Dictionary<Guid, DateTime?>();
            }

            // Get group UpdatedAt
            var groupUpdatedAt = await _db.Groups
                .Where(g => groupIds.Contains(g.GroupId))
                .Select(g => new { g.GroupId, g.UpdatedAt })
                .ToDictionaryAsync(x => x.GroupId, x => (DateTime?)x.UpdatedAt);

            // Get max task UpdatedAt per group
            var taskUpdatedAt = await _db.Tasks
                .Where(t => groupIds.Contains(t.GroupId!.Value))
                .GroupBy(t => t.GroupId)
                .Select(g => new { GroupId = g.Key!.Value, LastUpdated = g.Max(t => t.UpdatedAt) })
                .ToDictionaryAsync(x => x.GroupId, x => (DateTime?)x.LastUpdated);

            // Get max message CreatedAt per group
            var messageCreatedAt = await _db.GroupMessages
                .Where(m => groupIds.Contains(m.GroupId))
                .GroupBy(m => m.GroupId)
                .Select(g => new { GroupId = g.Key, LastMessage = g.Max(m => m.CreatedAt) })
                .ToDictionaryAsync(x => x.GroupId, x => (DateTime?)x.LastMessage);

            // Calculate max for each group
            var result = new Dictionary<Guid, DateTime?>();
            foreach (var groupId in groupIds)
            {
                var groupTime = groupUpdatedAt.GetValueOrDefault(groupId);
                var taskTime = taskUpdatedAt.GetValueOrDefault(groupId);
                var messageTime = messageCreatedAt.GetValueOrDefault(groupId);

                var maxTime = groupTime;
                if (taskTime.HasValue && (!maxTime.HasValue || taskTime > maxTime))
                {
                    maxTime = taskTime;
                }
                if (messageTime.HasValue && (!maxTime.HasValue || messageTime > maxTime))
                {
                    maxTime = messageTime;
                }

                result[groupId] = maxTime;
            }

            return result;
        }

        /// <summary>
        /// Get summary statistics for groups
        /// </summary>
        public async Task<GroupListSummary> GetGroupSummaryAsync(string? groupType)
        {
            var query = _db.Groups.AsQueryable();

            // Apply group type filter
            if (!string.IsNullOrWhiteSpace(groupType))
            {
                if (groupType.Equals("Studio", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(g => g.StudioId != null);
                }
                else if (groupType.Equals("Independent", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(g => g.StudioId == null);
                }
            }

            var summary = new GroupListSummary
            {
                TotalGroups = await query.CountAsync(),
                StudioGroups = await query.CountAsync(g => g.StudioId != null),
                IndependentGroups = await query.CountAsync(g => g.StudioId == null),
                ActiveGroups = await query.CountAsync(g => g.IsActive),
                InactiveGroups = await query.CountAsync(g => !g.IsActive)
            };

            return summary;
        }

        /// <summary>
        /// Get group by ID (including inactive groups for admin)
        /// </summary>
        public async Task<Group?> GetByIdAdminAsync(Guid groupId)
        {
            return await _db.Groups
                .FirstOrDefaultAsync(g => g.GroupId == groupId);
        }

        /// <summary>
        /// Get studio names for a list of studio IDs
        /// </summary>
        public async Task<Dictionary<Guid, string>> GetStudioNamesAsync(List<Guid?> studioIds)
        {
            var validIds = studioIds.Where(id => id.HasValue).Select(id => id!.Value).ToList();

            if (validIds.Count == 0)
            {
                return new Dictionary<Guid, string>();
            }

            var studios = await _db.Studios
                .Where(s => validIds.Contains(s.StudioId))
                .Select(s => new { s.StudioId, s.StudioName })
                .ToDictionaryAsync(x => x.StudioId, x => x.StudioName);

            return studios;
        }
    }
}
