using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IGroupService
    {
        Task<GroupListResponse> GetGroupsAsync(Guid userId);
        Task<GroupDetailResponse> GetGroupDetailAsync(Guid userId, Guid groupId);
        Task<GroupMemberListResponse> GetGroupMembersAsync(Guid userId, Guid groupId);
        Task<CreateGroupResponse> CreateGroupAsync(Guid userId, CreateGroupRequest request);
        Task DeleteGroupAsync(Guid userId, Guid groupId);
        Task<UpdateGroupResponse> UpdateGroupAsync(Guid userId, UpdateGroupRequest request);
        Task<CreateStudioGroupsResponse> CreateStudioGroupAsync(Guid userId, CreateStudioGroupsRequest request);
        Task<StudioGroupListResponse> GetStudioGroupsAsync(Guid userId, Guid studioId);
    }
}
