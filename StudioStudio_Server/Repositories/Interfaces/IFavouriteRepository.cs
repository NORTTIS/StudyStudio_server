using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IFavouriteRepository
    {
        Task<List<Favourite>> GetByUserAndGroupIdsAsync(Guid userId, List<Guid> groupIds);
    }
}
