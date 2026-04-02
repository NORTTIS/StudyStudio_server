using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Service interface cho qu?n l? members trong Group
    /// </summary>
    public interface IGroupMemberService
    {
        Task<RemoveMemberResponse> RemoveMemberAsync(Guid currentUserId, RemoveMemberRequest request);
        Task<AssignRoleResponse> AssignRoleAsync(Guid currentUserId, AssignRoleRequest request);
        Task<LeaveGroupResponse> LeaveGroupAsync(Guid userId, Guid groupId);

        // 🔹 ADDED: Reject pending member (kick without approval record)
        Task<RemoveMemberResponse> RejectMemberAsync(Guid currentUserId, Guid groupId, Guid targetUserId);
    }
}
