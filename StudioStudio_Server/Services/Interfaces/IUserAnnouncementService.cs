using StudioStudio_Server.Models.DTOs.Request;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IUserAnnouncementService
    {
        Task AddAnnouncementAsync(UserAnnouncementRequest request);
    }
}
