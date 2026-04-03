using Microsoft.AspNetCore.SignalR;
using StudioStudio_Server.Models.Entities;

using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IGroupRepository
    {
        Task<List<Group>> GetUserGroupsAsync(Guid userId);
        Task<Group?> GetByIdAsync(Guid groupId);
        Task<Group?> GetGroupWithDetailsAsync(Guid groupId);
        Task<bool> GroupNameExistsInStudioAsync(Guid? studioId, string groupName, Guid? userId);
        Task<int> CountGroupsCreatedByUserAsync(Guid userId);
        Task AddAsync(Group group);
        Task<bool> IsUserGroupOwnerAsync(Guid groupId, Guid userId);
        Task DeleteAsync(Group group);
        Task<int> GetGroupCountByStudioIdAsync(Guid studioId);
        Task UpdateAsync(Group group);
        Task<Group?> GetByIdForUpdateAsync(Guid groupId);
        Task<bool> GroupNameExistsInStudioExcludingGroupAsync(Guid? studioId, string groupName, Guid excludeGroupId);
        Task<List<Group>> GetStudioGroupsAsync(Guid studioId);
        Task<List<Group>> GetByIdsAsync(List<Guid> groupIds);
        Task<Guid> GetGroupOwnerIdAsync(Guid groupId);
        Task<List<string>> GetGroupNamesInStudioAsync(Guid? studioId);
        Task SaveChangesAsync();

        // Admin methods
        Task<(List<Group> Groups, int TotalCount)> GetGroupsAsync(
            string? searchTerm,
            string? groupType,
            int pageNumber,
            int pageSize);

        Task<Dictionary<Guid, int>> GetMemberCountsAsync(List<Guid> groupIds);
        Task<Dictionary<Guid, int>> GetTaskCountsAsync(List<Guid> groupIds);
        Task<Dictionary<Guid, DateTime?>> GetLastActivityAsync(List<Guid> groupIds);
        Task<GroupListSummary> GetGroupSummaryAsync(string? groupType);

        /// <summary>
        /// Get group by ID (including inactive groups for admin)
        /// </summary>
        Task<Group?> GetByIdAdminAsync(Guid groupId);

        /// <summary>
        /// Get studio names for a list of studio IDs
        /// </summary>
        Task<Dictionary<Guid, string>> GetStudioNamesAsync(List<Guid?> studioIds);

        /// <summary>
        /// Kiểm tra xem đã có group active nào của cùng owner và cùng studio có tên trùng không
        /// Chỉ kiểm tra group đang active (IsActive = true)
        /// </summary>
        Task<bool> HasActiveGroupWithNameAsync(Guid ownerId, Guid? studioId, string groupName, Guid excludeGroupId);
    }
}
