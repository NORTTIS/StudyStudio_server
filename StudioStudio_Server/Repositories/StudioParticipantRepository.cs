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
        /// Check if user is an approved member of studio
        /// Condition: StudioId = {studioId} AND UserId = {userId} AND IsApproved = true AND Studio.IsDeleted = false
        /// </summary>
        public async Task<bool> IsUserInStudioAsync(Guid studioId, Guid userId)
        {
            return await _context.StudioParticipants
                .Include(sp => sp.Studio)
                .AnyAsync(sp => sp.StudioId == studioId && sp.UserId == userId && sp.IsApproved && sp.Studio != null && !sp.Studio.IsDeleted);
        }

        /// <summary>
        /// Get approved participant record by StudioId and UserId
        /// Condition: StudioId = {studioId} AND UserId = {userId} AND IsApproved = true AND Studio.IsDeleted = false
        /// Use case: Check role, permissions
        /// </summary>
        public async Task<StudioParticipant?> GetByStudioAndUserAsync(Guid studioId, Guid userId)
        {
            return await _context.StudioParticipants
                .Include(sp => sp.Studio)
                .AsNoTracking()
                .FirstOrDefaultAsync(sp => sp.StudioId == studioId && sp.UserId == userId && sp.IsApproved && sp.Studio != null && !sp.Studio.IsDeleted);
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
        /// Get all approved participants in a studio
        /// Condition: StudioId = {studioId} AND IsApproved = true AND Studio.IsDeleted = false
        /// Use case: List all members in studio
        /// </summary>
        public async Task<List<StudioParticipant>> GetParticipantsByStudioIdAsync(Guid studioId)
        {
            return await _context.StudioParticipants
                .Include(sp => sp.Studio)
                .Where(sp => sp.StudioId == studioId && sp.IsApproved && sp.Studio != null && !sp.Studio.IsDeleted)
                .Include(sp => sp.User)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Get all studio participant records for a user
        /// Condition: UserId = {userId} AND IsApproved = true AND Studio.IsDeleted = false
        /// Use case: Get all studios where user is an approved participant (member)
        /// </summary>
        public async Task<List<StudioParticipant>> GetStudiosByUserIdAsync(Guid userId)
        {
            return await _context.StudioParticipants
                .Include(sp => sp.Studio)
                .Where(sp => sp.UserId == userId && sp.IsApproved && sp.Studio != null && !sp.Studio.IsDeleted)
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

        /// <summary>
        /// Update a participant record (e.g., set IsApproved = true)
        /// </summary>
        public async Task UpdateAsync(StudioParticipant participant)
        {
            var existingEntry = _context.ChangeTracker.Entries<StudioParticipant>()
                .FirstOrDefault(e => e.Entity.ParticipantId == participant.ParticipantId);

            if (existingEntry != null)
            {
                existingEntry.CurrentValues.SetValues(participant);
            }
            else
            {
                _context.StudioParticipants.Update(participant);
            }

            await _context.SaveChangesAsync();
        }

        // 🔹 ADDED: Pending membership & approval methods

        /// <summary>
        /// Get all pending (not yet approved) members of a studio
        /// Condition: StudioId = {studioId} AND IsApproved = false AND Studio.IsDeleted = false
        /// </summary>
        public async Task<List<StudioParticipant>> GetPendingByStudioIdAsync(Guid studioId)
        {
            return await _context.StudioParticipants
                .Include(sp => sp.User)
                .Where(sp => sp.StudioId == studioId && !sp.IsApproved &&
                    sp.Studio != null && !sp.Studio.IsDeleted)
                .OrderBy(sp => sp.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Check if user is an approved member of a studio
        /// Condition: StudioId+UserId in StudioParticipants AND IsApproved = true AND Studio.IsDeleted = false
        /// </summary>
        public async Task<bool> IsUserApprovedInStudioAsync(Guid studioId, Guid userId)
        {
            return await _context.StudioParticipants
                .AnyAsync(sp => sp.StudioId == studioId && sp.UserId == userId && sp.IsApproved &&
                    sp.Studio != null && !sp.Studio.IsDeleted);
        }

        /// <summary>
        /// Get pending participant record for a user in a studio (if any)
        /// Condition: StudioId+UserId in StudioParticipants AND IsApproved = false
        /// </summary>
        public async Task<StudioParticipant?> GetPendingByStudioAndUserAsync(Guid studioId, Guid userId)
        {
            return await _context.StudioParticipants
                .AsNoTracking()
                .FirstOrDefaultAsync(sp => sp.StudioId == studioId && sp.UserId == userId && !sp.IsApproved);
        }
    }
}
