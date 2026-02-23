using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IFavouriteRepository
    {
        Task<List<Favourite>> GetByUserAndGroupIdsAsync(Guid userId, List<Guid> groupIds);
        Task<Favourite?> GetByUserAndGroupIdAsync(Guid userId, Guid groupId);
        Task AddAsync(Favourite favourite);
        Task RemoveAsync(Favourite favourite);
        Task<bool> ExistsAsync(Guid userId, Guid groupId);
    }
}
