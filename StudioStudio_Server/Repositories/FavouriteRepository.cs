using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    public class FavouriteRepository : IFavouriteRepository
    {
        private readonly StudioDbContext _context;

        public FavouriteRepository(StudioDbContext context)
        {
            _context = context;
        }

        public async Task<List<Favourite>> GetByUserAndGroupIdsAsync(Guid userId, List<Guid> groupIds)
        {
            return await _context.Favourites
                .Where(f => f.UserId == userId && groupIds.Contains(f.GroupId))
                .OrderByDescending(f => f.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Favourite?> GetByUserAndGroupIdAsync(Guid userId, Guid groupId)
        {
            return await _context.Favourites
                .FirstOrDefaultAsync(f => f.UserId == userId && f.GroupId == groupId);
        }

        public async Task AddAsync(Favourite favourite)
        {
            _context.Favourites.Add(favourite);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveAsync(Favourite favourite)
        {
            _context.Favourites.Remove(favourite);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> ExistsAsync(Guid userId, Guid groupId)
        {
            return await _context.Favourites
                .AnyAsync(f => f.UserId == userId && f.GroupId == groupId);
        }

        public async Task<bool> IsFavouriteAsync(Guid userId, Guid groupId)
        {
            return await _context.Favourites
                .AnyAsync(f => f.UserId == userId && f.GroupId == groupId);
        }
    }
}
