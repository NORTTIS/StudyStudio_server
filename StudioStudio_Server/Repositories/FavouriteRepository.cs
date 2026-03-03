using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository handling operations with Favourite entity (user's favourite groups)
    /// </summary>
    public class FavouriteRepository : IFavouriteRepository
    {
        private readonly StudioDbContext _context;

        public FavouriteRepository(StudioDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get favourite record of user and group
        /// Condition: UserId = {userId} AND GroupId = {groupId}
        /// Use case: Check before add/remove favourite
        /// </summary>
        public async Task<Favourite?> GetByUserAndGroupIdAsync(Guid userId, Guid groupId)
        {
            return await _context.Favourites
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.UserId == userId && f.GroupId == groupId);
        }

        /// <summary>
        /// Get list of user's favourites (batch query for multiple groups)
        /// Condition: UserId = {userId} AND GroupId IN {groupIds}
        /// Order by: CreatedAt DESC
        /// Use case: Check favourite status for list of groups
        /// </summary>
        public async Task<List<Favourite>> GetByUserAndGroupIdsAsync(Guid userId, List<Guid> groupIds)
        {
            return await _context.Favourites
                .Where(f => f.UserId == userId && groupIds.Contains(f.GroupId))
                .OrderByDescending(f => f.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Check if group is in user's favourites
        /// Condition: UserId = {userId} AND GroupId = {groupId}
        /// </summary>
        public async Task<bool> IsFavouriteAsync(Guid userId, Guid groupId)
        {
            return await _context.Favourites
                .AnyAsync(f => f.UserId == userId && f.GroupId == groupId);
        }

        /// <summary>
        /// Check if favourite record exists (alias of IsFavouriteAsync)
        /// </summary>
        public async Task<bool> ExistsAsync(Guid userId, Guid groupId)
        {
            return await _context.Favourites
                .AnyAsync(f => f.UserId == userId && f.GroupId == groupId);
        }

        /// <summary>
        /// Add group to favourites
        /// </summary>
        public async Task AddAsync(Favourite favourite)
        {
            _context.Favourites.Add(favourite);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Remove group from favourites (hard delete)
        /// </summary>
        public async Task RemoveAsync(Favourite favourite)
        {
            _context.Favourites.Remove(favourite);
            await _context.SaveChangesAsync();
        }
    }
}
