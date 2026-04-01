using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository handling operations with StudioParticipant entity (members in studio)
    /// </summary>
    public class StudioParticipantRepository : IStudioParticipantRepository
    {
        private readonly StudioDbContext _context;

        public StudioParticipantRepository(StudioDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Add new participant to studio
        /// </summary>
        public async Task AddAsync(StudioParticipant participant)
        {
            _context.StudioParticipants.Add(participant);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Check if user is member of studio
        /// Condition: StudioId = {studioId} AND UserId = {userId} AND Studio.IsDeleted = false
        /// </summary>
        public async Task<bool> IsUserInStudioAsync(Guid studioId, Guid userId)
        {
            return await _context.StudioParticipants
                .Include(sp => sp.Studio)
                .AnyAsync(sp => sp.StudioId == studioId && sp.UserId == userId && sp.Studio != null && !sp.Studio.IsDeleted);
        }

        /// <summary>
        /// Get participant record by StudioId and UserId
        /// Condition: StudioId = {studioId} AND UserId = {userId} AND Studio.IsDeleted = false
        /// Use case: Check role, permissions
        /// </summary>
        public async Task<StudioParticipant?> GetByStudioAndUserAsync(Guid studioId, Guid userId)
        {
            return await _context.StudioParticipants
                .Include(sp => sp.Studio)
                .AsNoTracking()
                .FirstOrDefaultAsync(sp => sp.StudioId == studioId && sp.UserId == userId && sp.Studio != null && !sp.Studio.IsDeleted);
        }

        /// <summary>
        /// Count participants in studio
        /// Condition: StudioId = {studioId}
        /// Use case: Check member limit
        /// </summary>
        public async Task<int> GetParticipantCountByStudioIdAsync(Guid studioId)
        {
            return await _context.StudioParticipants
                .Where(sp => sp.StudioId == studioId)
                .CountAsync();
        }

        /// <summary>
        /// Get all participants in a studio
        /// Condition: StudioId = {studioId} AND Studio.IsDeleted = false
        /// Use case: List all members in studio
        /// </summary>
        public async Task<List<StudioParticipant>> GetParticipantsByStudioIdAsync(Guid studioId)
        {
            return await _context.StudioParticipants
                .Include(sp => sp.Studio)
                .Where(sp => sp.StudioId == studioId && sp.Studio != null && !sp.Studio.IsDeleted)
                .Include(sp => sp.User)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Get all studio participant records for a user
        /// Condition: UserId = {userId} AND Studio.IsDeleted = false
        /// Use case: Get all studios where user is a participant (member)
        /// </summary>
        public async Task<List<StudioParticipant>> GetStudiosByUserIdAsync(Guid userId)
        {
            return await _context.StudioParticipants
                .Include(sp => sp.Studio)
                .Where(sp => sp.UserId == userId && sp.Studio != null && !sp.Studio.IsDeleted)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Remove a participant from studio
        /// </summary>
        public async Task RemoveAsync(StudioParticipant participant)
        {
            _context.StudioParticipants.Remove(participant);
            await _context.SaveChangesAsync();
        }
    }
}
