using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IUserAnnouccementRepository
    {
        Task AddAsync(UserAnnouncement userAnnouncement);
        Task DeleteAsync(Guid userAnnouncementId);
        Task<List<UserAnnouncement>> GetByUserIdAsync(Guid userId);
        Task<UserAnnouncement?> GetByIdAsync(Guid userAnnouncementId);
        Task UpdateAsync(UserAnnouncement userAnnouncement);
    }
}
