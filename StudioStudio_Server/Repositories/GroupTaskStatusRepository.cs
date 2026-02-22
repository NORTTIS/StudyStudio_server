using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    public class GroupTaskStatusRepository : IGroupTaskStatusRepository
    {
        private readonly StudioDbContext _context;

        public GroupTaskStatusRepository(StudioDbContext context)
        {
            _context = context;
        }

        public async Task<List<GroupTaskStatus>> GetByGroupIdAsync(Guid groupId)
        {
            return await _context.GroupTaskStatuses
                .Where(s => s.GroupId == groupId)
                .OrderBy(s => s.Position)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task AddAsync(GroupTaskStatus status)
        {
            _context.GroupTaskStatuses.Add(status);
            await _context.SaveChangesAsync();
        }

        public async Task AddRangeAsync(List<GroupTaskStatus> statuses)
        {
            _context.GroupTaskStatuses.AddRange(statuses);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> ExistsAsync(Guid statusId)
        {
            return await _context.GroupTaskStatuses
                .AnyAsync(s => s.StatusId == statusId);
        }
    }
}
