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
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
