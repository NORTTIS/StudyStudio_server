using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Service interface cho quản lý Announcements (dành cho users)
    /// </summary>
    public interface IAnnouncementService
    {
        Task<List<AnnouncementResponse>> GetAllActiveAnnouncementsAsync(Guid userId);
        Task<AnnouncementResponse> GetAnnouncementByIdAsync(Guid announcementId);
        Task<List<UserAnnouncementResponse>> GetUserAnnouncementsAsync(Guid userId);
        Task MarkAnnouncementAsReadAsync(Guid userAnnouncementId, Guid userId);
        Task DeleteUserAnnouncementAsync(Guid userAnnouncementId, Guid userId);
    }
}
