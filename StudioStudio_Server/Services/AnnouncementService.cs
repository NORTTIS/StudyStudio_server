using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service handling business logic for Announcements (user side)
    /// Users can view active announcements and manage their personal announcement notifications
    /// OPTIMIZED: Uses caching to reduce database queries
    /// </summary>
    public class AnnouncementService(
        IAnnouncementRepository announcementRepository,
        IUserAnnouccementRepository userAnnouncementRepository,
        ICacheService cacheService) : IAnnouncementService
    {
        /// <summary>
        /// Get list of all announcements for user (both types combined)
        /// - Type 0-3: all active, with IsRead from UserAnnouncement
        /// - Type 4-17: only mentioned, with IsRead
        /// NO CACHE: Direct repository call for real-time filtering
        /// </summary>
        public async Task<List<AnnouncementResponse>> GetAllActiveAnnouncementsAsync(Guid userId)
        {
            var announcements = await announcementRepository.GetAllForUserAsync(userId);

            return announcements?
                .Select(a => MapToAnnouncementResponse(a, userId))
                .ToList() ?? new List<AnnouncementResponse>();
        }

        /// <summary>
        /// Get details of a specific announcement with user-specific IsRead
        /// Validate: Announcement must exist and be active (IsActive = true)
        /// </summary>
        public async Task<AnnouncementResponse> GetAnnouncementByIdAsync(Guid announcementId, Guid userId)
        {
            var announcement = await announcementRepository.GetByIdAsync(announcementId);

            if (announcement == null || !announcement.IsActive)
            {
                throw new AppException(
                    ErrorCodes.AnnouncementNotFound,
                    StatusCodes.Status404NotFound);
            }

            return MapToAnnouncementResponse(announcement, userId);
        }

        /// <summary>
        /// Mark announcement as read (for both type 0-3 and type 4-17)
        /// If UserAnnouncement record exists, update IsRead = true
        /// Otherwise, create new record to track that user has seen this announcement
        /// </summary>
        public async Task MarkAnnouncementAsReadAsync(Guid announcementId, Guid userId)
        {
            var existing = await userAnnouncementRepository
                .GetByAnnouncementAndUserAsync(announcementId, userId);

            if (existing != null)
            {
                if (!existing.IsRead)
                {
                    existing.IsRead = true;
                    await userAnnouncementRepository.UpdateAsync(existing);
                }
            }
            else
            {
                var newUA = new UserAnnouncement
                {
                    UserAnnouncementId = Guid.NewGuid(),
                    AnnouncementId = announcementId,
                    MentionedId = userId,
                    CreatedBy = userId,
                    IsRead = true,
                    IsDelete = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await userAnnouncementRepository.AddAsync(newUA);
            }

            await cacheService.InvalidateAnnouncementCachesAsync();
        }

        /// <summary>
        /// Soft delete user announcement (IsDelete = true in UserAnnouncement)
        /// Validate: UserAnnouncement must exist and belong to the user
        /// </summary>
        public async Task DeleteAnnouncementAsync(Guid announcementId, Guid userId)
        {
            // Find UserAnnouncement by AnnouncementId and userId
            var userAnnouncement = await userAnnouncementRepository
                .GetByAnnouncementAndUserAsync(announcementId, userId);

            if (userAnnouncement == null)
            {
                throw new AppException(
                    ErrorCodes.AnnouncementNotFound,
                    StatusCodes.Status404NotFound);
            }

            await userAnnouncementRepository.DeleteAsync(userAnnouncement.UserAnnouncementId);
            await cacheService.InvalidateAnnouncementCachesAsync();
        }

        /// <summary>
        /// Map Announcement entity to AnnouncementResponse DTO with user-specific IsRead
        /// </summary>
        private AnnouncementResponse MapToAnnouncementResponse(Announcement a, Guid userId)
        {
            var ua = a.UserAnnouncements?.FirstOrDefault(u => u.MentionedId == userId);

            return new AnnouncementResponse
            {
                AnnouncementId = a.AnnouncementId,
                Title = a.Title,
                Content = a.Content,
                Type = a.Type.ToString(),
                IsActive = a.IsActive,
                CreatedAt = a.CreatedAt,
                PublishedAt = a.PublishedAt,
                IsRead = ua?.IsRead ?? false,
                TaskId = a.TaskId,
                GroupId = a.GroupId,
                SourceType = a.SourceType
            };
        }
    }
}
