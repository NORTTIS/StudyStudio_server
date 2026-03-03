using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
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
        /// Check if group name exists in studio
        /// Condition: StudioId = {studioId} AND GroupName = {groupName} AND IsActive = true
        /// Use case: Validate when creating new group
        /// </summary>
        public async Task<bool> GroupNameExistsInStudioAsync(Guid? studioId, string groupName)
        {
            return await _db.Groups
                .AnyAsync(g => g.StudioId == studioId &&
                              g.GroupName == groupName &&
                              g.IsActive);
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
    }
}
