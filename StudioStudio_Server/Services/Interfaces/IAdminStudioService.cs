using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IAdminStudioService
    {
        /// <summary>
        /// Get paginated list of studios with filters for admin
        /// </summary>
        Task<AdminStudioListResponse> GetStudiosAsync(GetStudiosRequest request);

        /// <summary>
        /// Update studio status (activate/inactivate)
        /// </summary>
        Task UpdateStudioStatusAsync(Guid studioId, bool isActive);
    }
}
