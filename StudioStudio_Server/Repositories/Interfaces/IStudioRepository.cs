using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IStudioRepository
    {
        Task<List<Studio>> GetByIdsAsync(List<Guid> studioIds);
        Task<Studio?> GetByIdAsync(Guid studioId);
        Task<bool> IsUserStudioOwnerAsync(Guid studioId, Guid userId);
        Task<List<Studio>> GetByOwnerIdAsync(Guid ownerId);
        Task CreateStudioAsync(Studio newStudio);
        Task<int> CountStudioCreatedByUserAsync(Guid ownerId);
    }
}
