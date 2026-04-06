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
        /// Condition: GroupId = {groupId} AND UserId = {userId} AND Group.IsActive = true
        /// Use case: Check role, permissio ns
        /// </summary>
        public async Task<GroupParticipant?> GetByGroupAndUserAsync(Guid groupId, Guid userId)
        {
            return await _context.GroupParticipants
                .AsNoTracking()
                .FirstOrDefaultAsync(gp => gp.GroupId == groupId && gp.UserId == userId &&
                    _context.Groups.Any(g => g.GroupId == gp.GroupId && g.IsActive));
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
        /// Condition: GroupId = {groupId} AND UserId = {userId} AND Group.IsActive = true
        /// </summary>
        public async Task<GroupParticipant?> GetByUserAndGroupAsync(Guid userId, Guid groupId)
        {
            return await _context.GroupParticipants
                .AsNoTracking()
                .FirstOrDefaultAsync(gp => gp.GroupId == groupId && gp.UserId == userId &&
                    _context.Groups.Any(g => g.GroupId == gp.GroupId && g.IsActive));
        }

        /// <summary>
        /// Get all participants of group
        /// Condition: GroupId = {groupId} AND Group.IsActive = true
        /// Order by: CreatedAt ASC (oldest member first)
        /// </summary>
        public async Task<List<GroupParticipant>> GetAllByGroupIdAsync(Guid groupId)
        {
            return await _context.GroupParticipants
                .Where(gp => gp.GroupId == groupId && gp.IsApproved &&
                    _context.Groups.Any(g => g.GroupId == gp.GroupId && g.IsActive))
                .OrderBy(gp => gp.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Get participants for multiple groups (batch query)
        /// Condition: GroupId IN {groupIds} AND Group.IsActive = true
        /// Order by: CreatedAt ASC
        /// Use case: Load members info for list of groups
        /// </summary>
        public async Task<List<GroupParticipant>> GetByGroupIdsAsync(List<Guid> groupIds)
        {
            return await _context.GroupParticipants
                .Where(gp => groupIds.Contains(gp.GroupId) && gp.IsApproved &&
                    _context.Groups.Any(g => g.GroupId == gp.GroupId && g.IsActive))
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
                .Where(gp => gp.GroupId == groupId && gp.IsApproved)
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
                .Where(gp => gp.GroupId == groupId && gp.Role == role && gp.IsApproved)
                .CountAsync();
        }

        /// <summary>
        /// Check if user is member of group
        /// Condition: GroupId = {groupId} AND UserId = {userId} AND Group.IsActive = true
        /// </summary>
        public async Task<bool> IsUserInGroupAsync(Guid groupId, Guid userId)
        {
            return await _context.GroupParticipants
                .AnyAsync(gp => gp.GroupId == groupId && gp.UserId == userId &&
                    _context.Groups.Any(g => g.GroupId == gp.GroupId && g.IsActive));
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
        /// Condition: GroupId = {groupId} AND UserId = {userId} AND Group.IsActive = true
        /// </summary>
        public async Task<GroupRole> GetGroupRoleByUserIdAsync(Guid userId, Guid groupId)
        {
            var user = await _context.GroupParticipants
                .FirstOrDefaultAsync(gp => gp.UserId == userId && gp.GroupId == groupId &&
                    _context.Groups.Any(g => g.GroupId == gp.GroupId && g.IsActive));
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

        // 🔹 ADDED: Pending membership & approval methods

        /// <summary>
        /// Get all pending (not yet approved) members of a group
        /// Condition: GroupId = {groupId} AND IsApproved = false AND Group.IsActive = true
        /// </summary>
        public async Task<List<GroupParticipant>> GetPendingByGroupIdAsync(Guid groupId)
        {
            return await _context.GroupParticipants
                .Where(gp => gp.GroupId == groupId && !gp.IsApproved &&
                    _context.Groups.Any(g => g.GroupId == gp.GroupId && g.IsActive))
                .OrderBy(gp => gp.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Check if user is an approved member of a group
        /// Condition: GroupId+UserId in GroupParticipants AND IsApproved = true AND Group.IsActive = true
        /// </summary>
        public async Task<bool> IsUserApprovedInGroupAsync(Guid groupId, Guid userId)
        {
            return await _context.GroupParticipants
                .AnyAsync(gp => gp.GroupId == groupId && gp.UserId == userId && gp.IsApproved &&
                    _context.Groups.Any(g => g.GroupId == gp.GroupId && g.IsActive));
        }

        /// <summary>
        /// Get pending participant record for a user in a group (if any)
        /// Condition: GroupId+UserId in GroupParticipants AND IsApproved = false
        /// </summary>
        public async Task<GroupParticipant?> GetPendingByGroupAndUserAsync(Guid groupId, Guid userId)
        {
            return await _context.GroupParticipants
                .AsNoTracking()
                .FirstOrDefaultAsync(gp => gp.GroupId == groupId && gp.UserId == userId && !gp.IsApproved);
        }

        /// <summary>
        /// Get all pending (not yet approved) participants for multiple groups
        /// Condition: GroupId IN {groupIds} AND IsApproved = false AND Group.IsActive = true
        /// </summary>
        public async Task<List<GroupParticipant>> GetPendingByGroupIdsAsync(List<Guid> groupIds)
        {
            return await _context.GroupParticipants
                .Where(gp => groupIds.Contains(gp.GroupId) && !gp.IsApproved &&
                    _context.Groups.Any(g => g.GroupId == gp.GroupId && g.IsActive))
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
