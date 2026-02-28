using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository x? l? các thao tác v?i Favourite entity (user's favourite groups)
    /// </summary>
    public class FavouriteRepository : IFavouriteRepository
    {
        private readonly StudioDbContext _context;

        public FavouriteRepository(StudioDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// L?y favourite record c?a user và group
        /// Ði?u ki?n: UserId = {userId} AND GroupId = {groupId}
        /// Use case: Check trý?c khi add/remove favourite
        /// </summary>
        public async Task<Favourite?> GetByUserAndGroupIdAsync(Guid userId, Guid groupId)
        {
            return await _context.Favourites
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.UserId == userId && f.GroupId == groupId);
        }

        /// <summary>
        /// L?y danh sách favourites c?a user (batch query cho nhi?u groups)
        /// Ði?u ki?n: UserId = {userId} AND GroupId IN {groupIds}
        /// S?p x?p: CreatedAt DESC
        /// Use case: Check favourite status cho danh sách groups
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
        /// Ki?m tra group có trong favourites c?a user không
        /// Ði?u ki?n: UserId = {userId} AND GroupId = {groupId}
        /// </summary>
        public async Task<bool> IsFavouriteAsync(Guid userId, Guid groupId)
        {
            return await _context.Favourites
                .AnyAsync(f => f.UserId == userId && f.GroupId == groupId);
        }

        /// <summary>
        /// Ki?m tra favourite record có t?n t?i không (alias c?a IsFavouriteAsync)
        /// </summary>
        public async Task<bool> ExistsAsync(Guid userId, Guid groupId)
        {
            return await _context.Favourites
                .AnyAsync(f => f.UserId == userId && f.GroupId == groupId);
        }

        /// <summary>
        /// Thêm group vào favourites
        /// </summary>
        public async Task AddAsync(Favourite favourite)
        {
            _context.Favourites.Add(favourite);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Xóa group kh?i favourites (hard delete)
        /// </summary>
        public async Task RemoveAsync(Favourite favourite)
        {
            _context.Favourites.Remove(favourite);
            await _context.SaveChangesAsync();
        }
    }
}
