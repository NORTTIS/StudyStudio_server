using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Enums;

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
        Task<GroupTaskListResponse> GetGroupTasksAsync(
            Guid userId,
            Guid groupId,
            int page,
            int pageSize,
            string? search = null,
            Guid? assigneeId = null,
            Guid? statusId = null,
            TaskPriority? priority = null,
            TaskSeverity? severity = null,
            DateTime? startDateFrom = null,
            DateTime? startDateTo = null,
            DateTime? dueDateFrom = null,
            DateTime? dueDateTo = null,
            string? sortBy = "createdAt",
            bool sortAscending = true);

        // 🔹 ADDED: IsOpen toggle
        Task<ToggleIsOpenResponse> ToggleIsOpenAsync(Guid userId, Guid groupId, bool isOpen);

        // 🔹 ADDED: Pending members management
        Task<PendingMemberListResponse> GetPendingMembersAsync(Guid userId, Guid groupId);
        Task<ApproveMemberResponse> ApproveMemberAsync(Guid userId, Guid groupId, Guid targetUserId);
    }
}
