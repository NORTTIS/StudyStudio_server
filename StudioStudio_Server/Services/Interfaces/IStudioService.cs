using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.DTOs.Request;

namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Service interface cho qu?n l? Studios
    /// </summary>
    public interface IStudioService
    {
        Task<StudioListResponse> GetUserStudiosAsync(Guid userId);
        Task<StudioResponse> GetStudioDetailAsync(Guid userId, Guid studioId);
        Task<StudioResponse> CreateStudioAsync(Guid ownerId, CreateStudioRequest studio);
        Task DeleteStudioAsync(Guid ownerId, Guid studioId);
        Task<UpdateStudioResponse> UpdateStudioAsync(Guid userId, UpdateStudioRequest studio);
        Task<List<StudioMemberResponse>> GetStudioMembersAsync(Guid userId, Guid studioId);
        Task<LeaveStudioResponse> LeaveStudioAsync(Guid userId, Guid studioId);
        Task<ToggleIsOpenResponse> ToggleIsOpenAsync(Guid userId, Guid studioId, bool isOpen);
        Task<RemoveStudioMemberResponse> RemoveMemberAsync(Guid currentUserId, RemoveStudioMemberRequest request);
        Task<StudioPendingMemberListResponse> GetPendingMembersAsync(Guid userId, Guid studioId);
        Task<ApproveMemberResponse> ApproveMemberAsync(Guid userId, Guid studioId, Guid targetUserId);
        Task<ArchiveStudioResponse> ToggleArchiveStudioAsync(Guid userId, Guid studioId, bool isArchived);
    }
}
