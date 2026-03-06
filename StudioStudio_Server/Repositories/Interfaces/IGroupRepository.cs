using Microsoft.AspNetCore.SignalR;
using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IGroupRepository
    {
        Task<List<Group>> GetUserGroupsAsync(Guid userId);
        Task<Group?> GetByIdAsync(Guid groupId);
        Task<Group?> GetGroupWithDetailsAsync(Guid groupId);
        Task<bool> GroupNameExistsInStudioAsync(Guid? studioId, string groupName);
        Task<int> CountGroupsCreatedByUserAsync(Guid userId);
        Task AddAsync(Group group);
        Task<bool> IsUserGroupOwnerAsync(Guid groupId, Guid userId);
        Task DeleteAsync(Group group);
        Task<int> GetGroupCountByStudioIdAsync(Guid studioId);
        Task UpdateAsync(Group group);
        Task<bool> GroupNameExistsInStudioExcludingGroupAsync(Guid? studioId, string groupName, Guid excludeGroupId);
        Task<List<Group>> GetStudioGroupsAsync(Guid studioId);
        Task<Guid> GetGroupOwnerIdAsync(Guid groupId);
        Task<List<string>> GetGroupNamesInStudioAsync(Guid? studioId);
    }
}
