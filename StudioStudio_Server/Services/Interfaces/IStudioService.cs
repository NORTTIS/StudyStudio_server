using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IStudioService
    {
        Task<List<StudioResponse>> GetUserStudiosAsync(Guid userId);
    }
}
