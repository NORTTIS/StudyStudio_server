using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.DTOs.Request;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IStudioService
    {
        Task<List<StudioResponse>> GetUserStudiosAsync(Guid userId);
        Task<StudioResponse> CreateStudioAsync(Guid ownerId, CreateStudioRequest studio);
        Task<StudioResponse> GetStudioDetailAsync(Guid userId, Guid studioId);
        Task DeleteStudioAsync(Guid ownerId, Guid studioId);
        Task<UpdateStudioResponse> UpdateStudioAsync(Guid userId, UpdateStudioRequest studio);
    }
}
