using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IGroupService
    {
        Task<GroupListResponse> GetGroupsAsync(Guid userId);
        Task<CreateGroupResponse> CreateGroupAsync(Guid userId, CreateGroupRequest request);
        Task DeleteGroupAsync(Guid userId, Guid groupId);
        Task<UpdateGroupResponse> UpdateGroupAsync(Guid userId, UpdateGroupRequest request);
    }
}
