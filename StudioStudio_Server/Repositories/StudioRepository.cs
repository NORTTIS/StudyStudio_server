using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository x? l? các thao tác CRUD v?i Studio entity
    /// </summary>
    public class StudioRepository : IStudioRepository
    {
        private readonly StudioDbContext _context;

        public StudioRepository(StudioDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// L?y studio theo ID
        /// Ði?u ki?n: StudioId = {studioId}
        /// </summary>
        public async Task<Studio?> GetByIdAsync(Guid studioId)
        {
            return await _context.Studios
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudioId == studioId);
        }

        /// <summary>
        /// L?y nhi?u studios theo danh sách IDs
        /// Ði?u ki?n: StudioId IN {studioIds}
        /// Return: Empty list n?u studioIds r?ng
        /// </summary>
        public async Task<List<Studio>> GetByIdsAsync(List<Guid> studioIds)
        {
            if (studioIds.Count == 0)
            {
                return new List<Studio>();
            }

            return await _context.Studios
                .Where(s => studioIds.Contains(s.StudioId))
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// L?y danh sách studios c?a user
        /// Ði?u ki?n: OwnerId = {ownerId}
        /// S?p x?p: StudioName DESC, CreatedAt DESC
        /// </summary>
        public async Task<List<Studio>> GetByOwnerIdAsync(Guid ownerId)
        {
            return await _context.Studios
                .Where(s => s.OwnerId == ownerId)
                .OrderByDescending(s => s.StudioName)
                .ThenByDescending(s => s.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Ð?m s? studios do user t?o
        /// Ði?u ki?n: OwnerId = {userId}
        /// Use case: Check gi?i h?n s? studios có th? t?o
        /// </summary>
        public async Task<int> CountStudioCreatedByUserAsync(Guid userId)
        {
            return await _context.Studios
                .Where(s => s.OwnerId == userId)
                .CountAsync();
        }

        /// <summary>
        /// Ki?m tra user có ph?i owner c?a studio không
        /// Ði?u ki?n: StudioId = {studioId} AND OwnerId = {userId}
        /// </summary>
        public async Task<bool> IsUserStudioOwnerAsync(Guid studioId, Guid userId)
        {
            return await _context.Studios
                .AnyAsync(s => s.StudioId == studioId && s.OwnerId == userId);
        }

        /// <summary>
        /// Thêm studio m?i vào database
        /// </summary>
        public async Task CreateStudioAsync(Studio studio)
        {
            _context.Studios.Add(studio);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Update studio information
        /// </summary>
        public async Task UpdateStudioAsync(Studio studio)
        {
            _context.Studios.Update(studio);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Soft delete studio
        /// </summary>
        public async Task DeleteStudioAsync(Studio studio)
        {
            studio.IsDeleted = true;
            studio.UpdatedAt = DateTime.UtcNow;
            _context.Studios.Update(studio);
            await _context.SaveChangesAsync();
        }
    }
}
