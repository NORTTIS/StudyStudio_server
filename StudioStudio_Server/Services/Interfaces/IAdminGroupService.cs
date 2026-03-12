using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IAdminGroupService
    {
        /// <summary>
        /// Get paginated list of groups with filters for admin
        /// </summary>
        Task<AdminGroupListResponse> GetGroupsAsync(GetGroupsRequest request);

        /// <summary>
        /// Update group status (activate/inactivate)
        /// </summary>
        Task UpdateGroupStatusAsync(Guid groupId, bool isActive);
    }
}
