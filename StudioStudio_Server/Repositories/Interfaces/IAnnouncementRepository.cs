using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IAnnouncementRepository
    {
        Task AddAsync(Announcement announcement);
        Task<Announcement?> GetByIdAsync(Guid announcementId);
        Task<List<Announcement>> GetAllActiveAsync();
        Task<List<Announcement>> GetAllAsync();
        Task UpdateAsync(Announcement announcement);
        Task DeleteAsync(Announcement announcement);
    }
}
