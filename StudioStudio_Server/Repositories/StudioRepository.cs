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
        /// Condition: StudioId = {studioId} AND IsDeleted = false
        /// </summary>
        public async Task<Studio?> GetByIdAsync(Guid studioId)
        {
            return await _context.Studios
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudioId == studioId && !s.IsDeleted);
        }

        /// <summary>
        /// Get multiple studios by list of IDs
        /// Condition: StudioId IN {studioIds} AND IsDeleted = false
        /// Return: Empty list if studioIds is empty
        /// </summary>
        public async Task<List<Studio>> GetByIdsAsync(List<Guid> studioIds)
        {
            if (studioIds.Count == 0)
            {
                return new List<Studio>();
            }

            return await _context.Studios
                .Where(s => studioIds.Contains(s.StudioId) && !s.IsDeleted)
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
                .Where(s => s.OwnerId == ownerId && !s.IsDeleted)
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
                .Where(s => s.OwnerId == userId && !s.IsDeleted)
                .CountAsync();
        }

        /// <summary>
        /// Check if user is owner of studio
        /// Condition: StudioId = {studioId} AND OwnerId = {userId} AND IsDeleted = false
        /// </summary>
        public async Task<bool> IsUserStudioOwnerAsync(Guid studioId, Guid userId)
        {
            return await _context.Studios
                .AnyAsync(s => s.StudioId == studioId && s.OwnerId == userId && !s.IsDeleted);
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
        /// Get studio by ID with tracking (for updates)
        /// </summary>
        public async Task<Studio?> GetByIdForUpdateAsync(Guid studioId)
        {
            return await _context.Studios
                .FirstOrDefaultAsync(s => s.StudioId == studioId && !s.IsDeleted);
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

        /// <summary>
        /// Get groups belonging to a studio
        /// Condition: StudioId = {studioId} AND IsActive = true
        /// </summary>
        public async Task<List<Group>> GetGroupsByStudioIdAsync(Guid studioId)
        {
            return await _context.Groups
                .Where(g => g.StudioId == studioId && g.IsActive)
                .Include(g => g.Participants)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<bool> IsStudioNameExistByOwnerIdAsync(string studioName, Guid ownerId)
        {
            var trimmedName = studioName.Trim();
            return await _context.Studios
                .AnyAsync(s => s.StudioName.ToLower().Trim() == trimmedName.ToLower() && s.OwnerId == ownerId && !s.IsDeleted);
        }

        /// <summary>
        /// Check if studio name already exists for owner, excluding a specific studio (for update)
        /// </summary>
        public async Task<bool> IsStudioNameExistExcludingStudioAsync(string studioName, Guid ownerId, Guid excludeStudioId)
        {
            var trimmedName = studioName.Trim();
            return await _context.Studios
                .AnyAsync(s => s.StudioName.ToLower().Trim() == trimmedName.ToLower() && s.OwnerId == ownerId && !s.IsDeleted && s.StudioId != excludeStudioId);
        }

    }
}
