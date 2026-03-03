using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IGroupParticipantRepository
    {
        Task<List<GroupParticipant>> GetByGroupIdsAsync(List<Guid> groupIds);
        Task AddAsync(GroupParticipant participant);
        Task<int> GetParticipantCountByGroupIdAsync(Guid groupId);
        Task<bool> IsUserInGroupAsync(Guid groupId, Guid userId);
        Task<GroupParticipant?> GetByGroupAndUserAsync(Guid groupId, Guid userId);
        Task<GroupParticipant?> GetByUserAndGroupAsync(Guid userId, Guid groupId);
        Task RemoveAsync(GroupParticipant participant);
        Task UpdateAsync(GroupParticipant participant);
        Task<int> GetRoleCountByGroupIdAsync(Guid groupId, GroupRole role);
        Task<List<GroupParticipant>> GetAllByGroupIdAsync(Guid groupId);
        Task<GroupRole> GetGroupRoleByUserIdAsync(Guid userId, Guid groupId);
    }
}
