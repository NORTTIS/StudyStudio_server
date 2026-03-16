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
        /// Condition: StudioId = {studioId} AND UserId = {userId}
        /// </summary>
        public async Task<bool> IsUserInStudioAsync(Guid studioId, Guid userId)
        {
            return await _context.StudioParticipants
                .AnyAsync(sp => sp.StudioId == studioId && sp.UserId == userId);
        }

        /// <summary>
        /// Get participant record by StudioId and UserId
        /// Condition: StudioId = {studioId} AND UserId = {userId}
        /// Use case: Check role, permissions
        /// </summary>
        public async Task<StudioParticipant?> GetByStudioAndUserAsync(Guid studioId, Guid userId)
        {
            return await _context.StudioParticipants
                .AsNoTracking()
                .FirstOrDefaultAsync(sp => sp.StudioId == studioId && sp.UserId == userId);
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
        /// Condition: StudioId = {studioId}
        /// Use case: List all members in studio
        /// </summary>
        public async Task<List<StudioParticipant>> GetParticipantsByStudioIdAsync(Guid studioId)
        {
            return await _context.StudioParticipants
                .Where(sp => sp.StudioId == studioId)
                .Include(sp => sp.User)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
