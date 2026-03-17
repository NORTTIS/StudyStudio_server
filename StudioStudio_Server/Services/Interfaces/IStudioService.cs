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
    }
}
