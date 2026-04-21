using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository handling operations with Favourite entity (user's favourite groups)
    /// </summary>
    public class FavouriteRepository(StudioDbContext context) : IFavouriteRepository
    {
        /// <summary>
        /// Get favourite record of user and group
        /// Condition: UserId = {userId} AND GroupId = {groupId} AND Group.IsActive = true
        /// Use case: Check before add/remove favourite
        /// </summary>
        public async Task<Favourite?> GetByUserAndGroupIdAsync(Guid userId, Guid groupId)
        {
            var activeGroupIds = await context.Groups
                .Where(g => g.IsActive)
                .Select(g => g.GroupId)
                .ToListAsync();

            return await context.Favourites
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.UserId == userId && f.GroupId == groupId && activeGroupIds.Contains(f.GroupId));
        }

        /// <summary>
        /// Get list of user's favourites (batch query for multiple groups)
        /// Condition: UserId = {userId} AND GroupId IN {groupIds} AND Group.IsActive = true
        /// Order by: CreatedAt DESC
        /// Use case: Check favourite status for list of groups
        /// </summary>
        public async Task<List<Favourite>> GetByUserAndGroupIdsAsync(Guid userId, List<Guid> groupIds)
        {
            var activeGroupIds = await context.Groups
                .Where(g => g.IsActive)
                .Select(g => g.GroupId)
                .ToListAsync();

            return await context.Favourites
                .Where(f => f.UserId == userId && groupIds.Contains(f.GroupId) && activeGroupIds.Contains(f.GroupId))
                .OrderByDescending(f => f.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Check if group is in user's favourites
        /// Condition: UserId = {userId} AND GroupId = {groupId} AND Group.IsActive = true
        /// </summary>
        public async Task<bool> IsFavouriteAsync(Guid userId, Guid groupId)
        {
            var isActive = await context.Groups
                .AnyAsync(g => g.GroupId == groupId && g.IsActive);

            if (!isActive) return false;

            return await context.Favourites
                .AnyAsync(f => f.UserId == userId && f.GroupId == groupId);
        }

        /// <summary>
        /// Check if favourite record exists (alias of IsFavouriteAsync)
        /// Condition: UserId = {userId} AND GroupId = {groupId} AND Group.IsActive = true
        /// </summary>
        public async Task<bool> ExistsAsync(Guid userId, Guid groupId)
        {
            var isActive = await context.Groups
                .AnyAsync(g => g.GroupId == groupId && g.IsActive);

            if (!isActive) return false;

            return await context.Favourites
                .AnyAsync(f => f.UserId == userId && f.GroupId == groupId);
        }

        /// <summary>
        /// Add group to favourites
        /// </summary>
        public async Task AddAsync(Favourite favourite)
        {
            context.Favourites.Add(favourite);
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Remove group from favourites (hard delete)
        /// </summary>
        public async Task RemoveAsync(Favourite favourite)
        {
            context.Favourites.Remove(favourite);
            await context.SaveChangesAsync();
        }
    }
}
