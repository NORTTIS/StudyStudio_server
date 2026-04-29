using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Service interface cho quản lý Announcements (dành cho users)
    /// </summary>
    public interface IAnnouncementService
    {
        Task<AnnouncementListResponse> GetAllActiveAnnouncementsAsync(Guid userId, int page, int pageSize);
        Task<AnnouncementResponse> GetAnnouncementByIdAsync(Guid announcementId, Guid userId);
        Task MarkAnnouncementAsReadAsync(Guid announcementId, Guid userId);
        Task DeleteAnnouncementAsync(Guid announcementId, Guid userId);
    }
}
