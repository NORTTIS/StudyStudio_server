using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IUserAnnouccementRepository
    {
        Task AddAsync(UserAnnouncement userAnnouncement);
        Task DeleteAsync(Guid userAnnouncementId);
    }
}
