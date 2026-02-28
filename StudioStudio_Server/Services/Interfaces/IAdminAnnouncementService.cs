using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Service interface cho qu?n l? Announcements (dành cho Admin)
    /// </summary>
    public interface IAdminAnnouncementService
    {
        Task<List<AnnouncementResponse>> GetAllAnnouncementsAsync();
        Task<AnnouncementResponse> GetAnnouncementByIdAsync(Guid announcementId);
        Task<AnnouncementResponse> CreateAnnouncementAsync(Guid adminUserId, CreateAnnouncementRequest request);
        Task<AnnouncementResponse> UpdateAnnouncementAsync(Guid adminUserId, UpdateAnnouncementRequest request);
        Task DeleteAnnouncementAsync(Guid adminUserId, Guid announcementId);
    }
}
