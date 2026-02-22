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
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
