using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IStudioRepository
    {
        Task<List<Studio>> GetByIdsAsync(List<Guid> studioIds);
    }
}
