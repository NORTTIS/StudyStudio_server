using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository handling CRUD operations with Studio entity
    /// </summary>
    public class StudioRepository : IStudioRepository
    {
        private readonly StudioDbContext _context;

        public StudioRepository(StudioDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get studio by ID
        /// Condition: StudioId = {studioId}
        /// </summary>
        public async Task<Studio?> GetByIdAsync(Guid studioId)
        {
            return await _context.Studios
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudioId == studioId);
        }

        /// <summary>
        /// Get multiple studios by list of IDs
        /// Condition: StudioId IN {studioIds}
        /// Return: Empty list if studioIds is empty
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
        /// Get list of studios owned by user
        /// Condition: OwnerId = {ownerId}
        /// Order by: StudioName DESC, CreatedAt DESC
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
        /// Count studios created by user
        /// Condition: OwnerId = {userId}
        /// Use case: Check studio creation limit
        /// </summary>
        public async Task<int> CountStudioCreatedByUserAsync(Guid userId)
        {
            return await _context.Studios
                .Where(s => s.OwnerId == userId)
                .CountAsync();
        }

        /// <summary>
        /// Check if user is owner of studio
        /// Condition: StudioId = {studioId} AND OwnerId = {userId}
        /// </summary>
        public async Task<bool> IsUserStudioOwnerAsync(Guid studioId, Guid userId)
        {
            return await _context.Studios
                .AnyAsync(s => s.StudioId == studioId && s.OwnerId == userId);
        }

        /// <summary>
        /// Add new studio to database
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
        /// Hard delete studio
        /// Note: Cascade delete will remove all groups belonging to studio
        /// </summary>
        public async Task DeleteStudioAsync(Studio studio)
        {
            _context.Studios.Remove(studio);
            await _context.SaveChangesAsync();
        }
    }
}
