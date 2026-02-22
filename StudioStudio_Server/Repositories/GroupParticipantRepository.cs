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
    }
}
