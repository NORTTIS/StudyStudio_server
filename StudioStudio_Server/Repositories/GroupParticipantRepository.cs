using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository x? l? các thao tác v?i GroupParticipant entity (members trong group)
    /// </summary>
    public class GroupParticipantRepository : IGroupParticipantRepository
    {
        private readonly StudioDbContext _context;

        public GroupParticipantRepository(StudioDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// L?y participant record theo GroupId và UserId
        /// Ði?u ki?n: GroupId = {groupId} AND UserId = {userId}
        /// Use case: Check role, permissions
        /// </summary>
        public async Task<GroupParticipant?> GetByGroupAndUserAsync(Guid groupId, Guid userId)
        {
            return await _context.GroupParticipants
                .AsNoTracking()
                .FirstOrDefaultAsync(gp => gp.GroupId == groupId && gp.UserId == userId);
        }

        /// <summary>
        /// L?y participant record theo UserId và GroupId (alias c?a GetByGroupAndUserAsync)
        /// </summary>
        public async Task<GroupParticipant?> GetByUserAndGroupAsync(Guid userId, Guid groupId)
        {
            return await _context.GroupParticipants
                .AsNoTracking()
                .FirstOrDefaultAsync(gp => gp.GroupId == groupId && gp.UserId == userId);
        }

        /// <summary>
        /// L?y t?t c? participants c?a group
        /// Ði?u ki?n: GroupId = {groupId}
        /// S?p x?p: CreatedAt ASC (member c? nh?t trý?c)
        /// </summary>
        public async Task<List<GroupParticipant>> GetAllByGroupIdAsync(Guid groupId)
        {
            return await _context.GroupParticipants
                .Where(gp => gp.GroupId == groupId)
                .OrderBy(gp => gp.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// L?y participants cho nhi?u groups (batch query)
        /// Ði?u ki?n: GroupId IN {groupIds}
        /// S?p x?p: CreatedAt ASC
        /// Use case: Load members info cho danh sách groups
        /// </summary>
        public async Task<List<GroupParticipant>> GetByGroupIdsAsync(List<Guid> groupIds)
        {
            return await _context.GroupParticipants
                .Where(gp => groupIds.Contains(gp.GroupId))
                .OrderBy(gp => gp.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Ð?m s? participants trong group
        /// Ði?u ki?n: GroupId = {groupId}
        /// Use case: Check gi?i h?n members
        /// </summary>
        public async Task<int> GetParticipantCountByGroupIdAsync(Guid groupId)
        {
            return await _context.GroupParticipants
                .Where(gp => gp.GroupId == groupId)
                .CountAsync();
        }

        /// <summary>
        /// Ð?m s? members có role c? th? trong group
        /// Ði?u ki?n: GroupId = {groupId} AND Role = {role}
        /// Use case: Check s? Moderators (ch? cho phép 1), s? Owners (luôn 1)
        /// </summary>
        public async Task<int> GetRoleCountByGroupIdAsync(Guid groupId, GroupRole role)
        {
            return await _context.GroupParticipants
                .Where(gp => gp.GroupId == groupId && gp.Role == role)
                .CountAsync();
        }

        /// <summary>
        /// Ki?m tra user có ph?i member c?a group không
        /// Ði?u ki?n: GroupId = {groupId} AND UserId = {userId}
        /// </summary>
        public async Task<bool> IsUserInGroupAsync(Guid groupId, Guid userId)
        {
            return await _context.GroupParticipants
                .AnyAsync(gp => gp.GroupId == groupId && gp.UserId == userId);
        }

        /// <summary>
        /// Thêm participant m?i vào group
        /// </summary>
        public async Task AddAsync(GroupParticipant participant)
        {
            _context.GroupParticipants.Add(participant);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Update participant information (ch? y?u là Role)
        /// </summary>
        public async Task UpdateAsync(GroupParticipant participant)
        {
            _context.GroupParticipants.Update(participant);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Xóa participant kh?i group (hard delete)
        /// Use case: Leave group, kick member
        /// </summary>
        public async Task RemoveAsync(GroupParticipant participant)
        {
            _context.GroupParticipants.Remove(participant);
            await _context.SaveChangesAsync();
        }
    }
}
