using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    public class StudioRepository : IStudioRepository
    {
        private readonly StudioDbContext _context;

        public StudioRepository(StudioDbContext context)
        {
            _context = context;
        }

        public async Task<List<Studio>> GetByIdsAsync(List<Guid> studioIds)
        {
            if (studioIds.Count == 0)
                return new List<Studio>();

            return await _context.Studios
                .Where(s => studioIds.Contains(s.StudioId))
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Studio?> GetByIdAsync(Guid studioId)
        {
            return await _context.Studios
                .FirstOrDefaultAsync(s => s.StudioId == studioId);
        }

        public async Task<bool> IsUserStudioOwnerAsync(Guid studioId, Guid userId)
        {
            return await _context.Studios
                .AnyAsync(s => s.StudioId == studioId && s.OwnerId == userId);
        }

        public async Task<List<Studio>> GetByOwnerIdAsync(Guid ownerId)
        {
            return await _context.Studios
                .Where(s => s.OwnerId == ownerId)
                .OrderByDescending(s => s.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
