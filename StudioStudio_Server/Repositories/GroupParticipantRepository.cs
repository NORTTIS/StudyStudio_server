using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    public class GroupParticipantRepository : IGroupParticipantRepository
    {
        private readonly StudioDbContext _context;

        public GroupParticipantRepository(StudioDbContext context)
        {
            _context = context;
        }

        public async Task<List<GroupParticipant>> GetByGroupIdsAsync(List<Guid> groupIds)
        {
            return await _context.GroupParticipants
                .Where(gp => groupIds.Contains(gp.GroupId))
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task AddAsync(GroupParticipant participant)
        {
            _context.GroupParticipants.Add(participant);
            await _context.SaveChangesAsync();
        }

        public async Task<int> GetParticipantCountByGroupIdAsync(Guid groupId)
        {
            return await _context.GroupParticipants
                .Where(gp => gp.GroupId == groupId)
                .CountAsync();
        }

        public async Task<bool> IsUserInGroupAsync(Guid groupId, Guid userId)
        {
            return await _context.GroupParticipants
                .AnyAsync(gp => gp.GroupId == groupId && gp.UserId == userId);
        }

        public async Task<GroupParticipant?> GetByGroupAndUserAsync(Guid groupId, Guid userId)
        {
            return await _context.GroupParticipants
                .FirstOrDefaultAsync(gp => gp.GroupId == groupId && gp.UserId == userId);
        }

        public async Task RemoveAsync(GroupParticipant participant)
        {
            _context.GroupParticipants.Remove(participant);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(GroupParticipant participant)
        {
            _context.GroupParticipants.Update(participant);
            await _context.SaveChangesAsync();
        }

        public async Task<int> GetRoleCountByGroupIdAsync(Guid groupId, GroupRole role)
        {
            return await _context.GroupParticipants
                .Where(gp => gp.GroupId == groupId && gp.Role == role)
                .CountAsync();
        }

        public async Task<List<GroupParticipant>> GetAllByGroupIdAsync(Guid groupId)
        {
            return await _context.GroupParticipants
                .Where(gp => gp.GroupId == groupId)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
