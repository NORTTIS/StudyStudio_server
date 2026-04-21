using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IUserAnnouccementRepository
    {
        Task AddAsync(UserAnnouncement userAnnouncement);
        Task DeleteAsync(Guid userAnnouncementId);
        Task<UserAnnouncement?> GetByIdAsync(Guid userAnnouncementId);
        Task<UserAnnouncement?> GetByAnnouncementAndUserAsync(Guid announcementId, Guid userId);
        Task UpdateAsync(UserAnnouncement userAnnouncement);
    }
}
