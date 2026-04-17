using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.Diagnostics;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service handling business logic for User Announcements (personalized announcements)
    /// Manages announcements targeted to specific users (mentions/notifications)
    /// Note: This is different from general Announcements - these are user-specific
    /// </summary>
    public class UserAnnouncementService(IUserAnnouccementRepository userAnnouccementRepository) : IUserAnnouncementService
    {
        private readonly IUserAnnouccementRepository _userAnnouccementRepository = userAnnouccementRepository;

        /// <summary>
        /// Add new user announcement (mention/notification)
        /// Creates a personalized announcement record for specific user
        /// Initial state: IsRead = false, IsDelete = false
        /// </summary>
        public async Task AddAnnouncementAsync(UserAnnouncementRequest request)
        {
            var newAnnouce = new UserAnnouncement
            {
                UserAnnouncementId = Guid.NewGuid(),
                AnnouncementId = request.AnnouncementId,
                MentionedId = request.MentionedId,
                CreatedBy = request.CreatedBy,  // Người tạo thông báo
                CreatedAt = request.CreatedAt,
                IsRead = request.IsRead,
                IsDelete = false,
                UpdatedAt = request.CreatedAt
            };
            await _userAnnouccementRepository.AddAsync(newAnnouce);
        }

        /// <summary>
        /// Remove user announcement (soft delete)
        /// Sets IsDelete = true in repository
        /// Use case: User dismisses notification
        /// </summary>
        public async Task RemoveAnnouncementAsync(Guid announceId)
        {
            await _userAnnouccementRepository.DeleteAsync(announceId);
        }
    }
}
