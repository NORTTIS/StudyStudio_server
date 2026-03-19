using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository handling operations with GroupParticipant entity (members in group)
    /// </summary>
    public class GroupParticipantRepository : IGroupParticipantRepository
    {
        private readonly StudioDbContext _context;

        public GroupParticipantRepository(StudioDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get participant record by GroupId and UserId
        /// Condition: GroupId = {groupId} AND UserId = {userId}
        /// Use case: Check role, permissions
        /// </summary>
        public async Task<GroupParticipant?> GetByGroupAndUserAsync(Guid groupId, Guid userId)
        {
            return await _context.GroupParticipants
                .AsNoTracking()
                .FirstOrDefaultAsync(gp => gp.GroupId == groupId && gp.UserId == userId);
        }

        /// <summary>
        /// Get participant record by GroupId and UserId with tracking enabled
        /// Condition: GroupId = {groupId} AND UserId = {userId}
        /// Use case: For updates
        /// </summary>
        public async Task<GroupParticipant?> GetByGroupAndUserTrackedAsync(Guid groupId, Guid userId)
        {
            return await _context.GroupParticipants
                .FirstOrDefaultAsync(gp => gp.GroupId == groupId && gp.UserId == userId);
        }

        /// <summary>
        /// Get participant record by UserId and GroupId (alias of GetByGroupAndUserAsync)
        /// </summary>
        public async Task<GroupParticipant?> GetByUserAndGroupAsync(Guid userId, Guid groupId)
        {
            return await _context.GroupParticipants
                .AsNoTracking()
                .FirstOrDefaultAsync(gp => gp.GroupId == groupId && gp.UserId == userId);
        }

        /// <summary>
        /// Get all participants of group
        /// Condition: GroupId = {groupId}
        /// Order by: CreatedAt ASC (oldest member first)
        /// </summary>
        public async Task<List<GroupParticipant>> GetAllByGroupIdAsync(Guid groupId)
        {
            return await _context.GroupParticipants
                .Where(gp => gp.GroupId == groupId)
                .OrderBy(gp => gp.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Get participants for multiple groups (batch query)
        /// Condition: GroupId IN {groupIds}
        /// Order by: CreatedAt ASC
        /// Use case: Load members info for list of groups
        /// </summary>
        public async Task<List<GroupParticipant>> GetByGroupIdsAsync(List<Guid> groupIds)
        {
            return await _context.GroupParticipants
                .Where(gp => groupIds.Contains(gp.GroupId))
                .OrderBy(gp => gp.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Count participants in group
        /// Condition: GroupId = {groupId}
        /// Use case: Check member limit
        /// </summary>
        public async Task<int> GetParticipantCountByGroupIdAsync(Guid groupId)
        {
            return await _context.GroupParticipants
                .Where(gp => gp.GroupId == groupId)
                .CountAsync();
        }

        /// <summary>
        /// Count members with specific role in group
        /// Condition: GroupId = {groupId} AND Role = {role}
        /// Use case: Check number of Moderators (only allow 1), Owners (always 1)
        /// </summary>
        public async Task<int> GetRoleCountByGroupIdAsync(Guid groupId, GroupRole role)
        {
            return await _context.GroupParticipants
                .Where(gp => gp.GroupId == groupId && gp.Role == role)
                .CountAsync();
        }

        /// <summary>
        /// Check if user is member of group
        /// Condition: GroupId = {groupId} AND UserId = {userId}
        /// </summary>
        public async Task<bool> IsUserInGroupAsync(Guid groupId, Guid userId)
        {
            return await _context.GroupParticipants
                .AnyAsync(gp => gp.GroupId == groupId && gp.UserId == userId);
        }

        /// <summary>
        /// Add new participant to group
        /// </summary>
        public async Task AddAsync(GroupParticipant participant)
        {
            _context.GroupParticipants.Add(participant);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Update participant information (mainly Role)
        /// </summary>
        public async Task UpdateAsync(GroupParticipant participant)
        {
            var existingEntry = _context.ChangeTracker.Entries<GroupParticipant>()
                .FirstOrDefault(e => e.Entity.ParticipantId == participant.ParticipantId);

            if (existingEntry != null)
            {
                existingEntry.CurrentValues.SetValues(participant);
            }
            else
            {
                _context.GroupParticipants.Update(participant);
            }
            
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Delete participant from group (hard delete)
        /// Use case: Leave group, kick member
        /// </summary>
        public async Task RemoveAsync(GroupParticipant participant)
        {
            var existingEntry = _context.ChangeTracker.Entries<GroupParticipant>()
                .FirstOrDefault(e => e.Entity.ParticipantId == participant.ParticipantId);

            if (existingEntry != null)
            {
                _context.GroupParticipants.Remove(existingEntry.Entity);
            }
            else
            {
                _context.GroupParticipants.Remove(participant);
            }
            
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Get the role of the user in the group
        /// </summary>
        public async Task<GroupRole> GetGroupRoleByUserIdAsync(Guid userId, Guid groupId)
        {
            var user = await _context.GroupParticipants.FirstOrDefaultAsync(
                u => u.UserId == userId &&
                u.GroupId == groupId);
            return user?.Role ?? GroupRole.Viewer;
        }

        /// <summary>
        /// Add multiple participants in a single batch
        /// </summary>
        public async Task AddRangeAsync(IEnumerable<GroupParticipant> participants)
        {
            _context.GroupParticipants.AddRange(participants);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Update multiple participants in a single batch
        /// </summary>
        public async Task UpdateRangeAsync(IEnumerable<GroupParticipant> participants)
        {
            _context.GroupParticipants.UpdateRange(participants);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Remove multiple participants in a single batch
        /// </summary>
        public async Task RemoveRangeAsync(IEnumerable<GroupParticipant> participants)
        {
            _context.GroupParticipants.RemoveRange(participants);
            await _context.SaveChangesAsync();
        }
    }
}
