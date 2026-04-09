using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IGroupParticipantRepository
    {
        Task<List<GroupParticipant>> GetByGroupIdsAsync(List<Guid> groupIds);
        Task<Dictionary<Guid, int>> GetParticipantCountsBatchAsync(List<Guid> groupIds);
        Task AddAsync(GroupParticipant participant);
        Task<int> GetParticipantCountByGroupIdAsync(Guid groupId);
        Task<bool> IsUserInGroupAsync(Guid groupId, Guid userId);
        Task<GroupParticipant?> GetByGroupAndUserAsync(Guid groupId, Guid userId);
        Task<GroupParticipant?> GetByGroupAndUserTrackedAsync(Guid groupId, Guid userId);
        Task<GroupParticipant?> GetByUserAndGroupAsync(Guid userId, Guid groupId);
        Task RemoveAsync(GroupParticipant participant);
        Task UpdateAsync(GroupParticipant participant);
        Task<int> GetRoleCountByGroupIdAsync(Guid groupId, GroupRole role);
        Task<List<GroupParticipant>> GetAllByGroupIdAsync(Guid groupId);
        Task<GroupRole> GetGroupRoleByUserIdAsync(Guid userId, Guid groupId);
        Task AddRangeAsync(IEnumerable<GroupParticipant> participants);
        Task UpdateRangeAsync(IEnumerable<GroupParticipant> participants);
        Task RemoveRangeAsync(IEnumerable<GroupParticipant> participants);

        // 🔹 ADDED: Pending membership & approval methods
        /// <summary>
        /// Get all pending (not yet approved) members of a group
        /// Condition: GroupId = {groupId} AND IsApproved = false
        /// </summary>
        Task<List<GroupParticipant>> GetPendingByGroupIdAsync(Guid groupId);

        /// <summary>
        /// Check if user is an approved member of a group
        /// Condition: GroupId+UserId in GroupParticipants AND IsApproved = true AND Group.IsActive = true
        /// </summary>
        Task<bool> IsUserApprovedInGroupAsync(Guid groupId, Guid userId);

        /// <summary>
        /// Get pending participant record for a user in a group (if any)
        /// Condition: GroupId+UserId in GroupParticipants AND IsApproved = false
        /// </summary>
        Task<GroupParticipant?> GetPendingByGroupAndUserAsync(Guid groupId, Guid userId);

        /// <summary>
        /// Get all pending (not yet approved) participants for multiple groups
        /// Condition: GroupId IN {groupIds} AND IsApproved = false AND Group.IsActive = true
        /// </summary>
        Task<List<GroupParticipant>> GetPendingByGroupIdsAsync(List<Guid> groupIds);
    }
}
