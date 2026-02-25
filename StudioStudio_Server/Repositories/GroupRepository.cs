using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    public class GroupRepository : IGroupRepository
    {
        private readonly StudioDbContext _db;

        public GroupRepository(StudioDbContext db)
        {
            _db = db;
        }

        public async Task<List<Group>> GetUserGroupsAsync(Guid userId)
        {
            return await _db.Groups
                .Where(g => g.Participants.Any(p => p.UserId == userId) && g.IsActive)
                .Include(g => g.Participants)
                .OrderByDescending(g => g.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Group?> GetByIdAsync(Guid groupId)
        {
            return await _db.Groups
                .Include(g => g.Participants)
                .FirstOrDefaultAsync(g => g.GroupId == groupId && g.IsActive);
        }

        public async Task<Group?> GetGroupWithDetailsAsync(Guid groupId)
        {
            return await _db.Groups
                .Include(g => g.Participants)
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.GroupId == groupId && g.IsActive);
        }

        public async Task<bool> GroupNameExistsInStudioAsync(Guid? studioId, string groupName)
        {
            return await _db.Groups
                .AnyAsync(g => g.StudioId == studioId &&
                              g.GroupName == groupName &&
                              g.IsActive);
        }

        public async Task<int> CountGroupsCreatedByUserAsync(Guid userId)
        {
            return await _db.Groups
                .Where(g => g.Participants.Any(p => p.UserId == userId && p.Role == GroupRole.Owner) && g.IsActive)
                .CountAsync();
        }

        public async Task AddAsync(Group group)
        {
            _db.Groups.Add(group);
            await _db.SaveChangesAsync();
        }

        public async Task<bool> IsUserGroupOwnerAsync(Guid groupId, Guid userId)
        {
            return await _db.Groups
                .Where(g => g.GroupId == groupId && g.IsActive)
                .AnyAsync(g => g.Participants.Any(p => p.UserId == userId && p.Role == GroupRole.Owner));
        }

        public async Task DeleteAsync(Group group)
        {
            group.IsActive = false;
            group.UpdatedAt = DateTime.UtcNow;
            _db.Groups.Update(group);
            await _db.SaveChangesAsync();
        }

        public async Task<int> GetGroupCountByStudioIdAsync(Guid studioId)
        {
            return await _db.Groups
                .Where(g => g.StudioId == studioId && g.IsActive)
                .CountAsync();
        }

        public async Task UpdateAsync(Group group)
        {
            group.UpdatedAt = DateTime.UtcNow;
            _db.Groups.Update(group);
            await _db.SaveChangesAsync();
        }

        public async Task<bool> GroupNameExistsInStudioExcludingGroupAsync(Guid? studioId, string groupName, Guid excludeGroupId)
        {
            return await _db.Groups
                .AnyAsync(g => g.StudioId == studioId &&
                              g.GroupName == groupName &&
                              g.GroupId != excludeGroupId &&
                              g.IsActive);
        }

        public async Task<List<Group>> GetStudioGroupsAsync(Guid studioId)
        {
            return await _db.Groups
                .Where(g => g.StudioId == studioId && g.IsActive)
                .OrderByDescending(g => g.GroupName)
                .ThenByDescending(g => g.CreatedAt)
                .ToListAsync();
        }
    }
}
