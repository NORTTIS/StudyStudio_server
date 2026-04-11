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
    public class AnnouncementService : IAnnouncementService
    {
        private readonly IAnnouncementRepository _announcementRepository;
        private readonly IUserAnnouccementRepository _userAnnouncementRepository;
        private readonly ICacheService _cacheService;

        public AnnouncementService(
            IAnnouncementRepository announcementRepository,
            IUserAnnouccementRepository userAnnouncementRepository,
            ICacheService cacheService)
        {
            _announcementRepository = announcementRepository;
            _userAnnouncementRepository = userAnnouncementRepository;
            _cacheService = cacheService;
        }

        /// <summary>
        /// Get list of all announcements for user (both types combined)
        /// - Type 0-3: all active, with IsRead from UserAnnouncement
        /// - Type 4-17: only mentioned, with IsRead
        /// NO CACHE: Direct repository call for real-time filtering
        /// </summary>
        public async Task<List<AnnouncementResponse>> GetAllActiveAnnouncementsAsync(Guid userId)
        {
            var announcements = await _announcementRepository.GetAllForUserAsync(userId);

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
            var announcement = await _announcementRepository.GetByIdAsync(announcementId);

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
            var existing = await _userAnnouncementRepository
                .GetByAnnouncementAndUserAsync(announcementId, userId);

            if (existing != null)
            {
                if (!existing.IsRead)
                {
                    existing.IsRead = true;
                    await _userAnnouncementRepository.UpdateAsync(existing);
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
                await _userAnnouncementRepository.AddAsync(newUA);
            }

            await _cacheService.InvalidateAnnouncementCachesAsync();
        }

        /// <summary>
        /// Soft delete user announcement (IsDelete = true in UserAnnouncement)
        /// Validate: UserAnnouncement must exist and belong to the user
        /// </summary>
        public async Task DeleteAnnouncementAsync(Guid announcementId, Guid userId)
        {
            // Find UserAnnouncement by AnnouncementId and userId
            var userAnnouncement = await _userAnnouncementRepository
                .GetByAnnouncementAndUserAsync(announcementId, userId);

            if (userAnnouncement == null)
            {
                throw new AppException(
                    ErrorCodes.AnnouncementNotFound,
                    StatusCodes.Status404NotFound);
            }

            await _userAnnouncementRepository.DeleteAsync(userAnnouncement.UserAnnouncementId);
            await _cacheService.InvalidateAnnouncementCachesAsync();
        }

        /// <summary>
        /// Validate user owns the user announcement
        /// Checks if MentionedId matches userId
        /// </summary>
        private void ValidateUserAnnouncementOwnership(UserAnnouncement userAnnouncement, Guid userId)
        {
            if (userAnnouncement.MentionedId != userId)
            {
                throw new AppException(
                    ErrorCodes.AuthForbidden,
                    StatusCodes.Status403Forbidden);
            }
        }

        /// <summary>
        /// Get announcements by list of IDs
        /// Returns: Dictionary with AnnouncementId as key
        /// Helper method for joining user announcements with announcement details
        /// OPTIMIZED: Use bulk query instead of N+1 loop
        /// </summary>
        private async Task<Dictionary<Guid, Announcement>> GetAnnouncementsByIdsAsync(List<Guid> announcementIds)
        {
            if (announcementIds == null || !announcementIds.Any())
            {
                return new Dictionary<Guid, Announcement>();
            }

            // Bulk load all announcements in ONE query instead of N queries
            var announcements = await _announcementRepository.GetByIdsAsync(announcementIds);

            return announcements.ToDictionary(a => a.AnnouncementId);
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

        /// <summary>
        /// Map UserAnnouncement + Announcement to UserAnnouncementResponse DTO
        /// </summary>
        private UserAnnouncementResponse MapToUserAnnouncementResponse(
            UserAnnouncement userAnnouncement,
            Announcement? announcement)
        {
            return new UserAnnouncementResponse
            {
                UserAnnouncementId = userAnnouncement.UserAnnouncementId,
                AnnouncementId = userAnnouncement.AnnouncementId,
                Title = announcement?.Title ?? "",
                Content = announcement?.Content ?? "",
                Type = announcement?.Type.ToString() ?? "",
                IsRead = userAnnouncement.IsRead,
                CreatedAt = userAnnouncement.CreatedAt,
                PublishedAt = announcement?.PublishedAt,
                MentionedId = userAnnouncement.MentionedId,
                CreatedBy = userAnnouncement.CreatedBy,
                TaskId = announcement?.TaskId,
                GroupId = announcement?.GroupId,
                SourceType = announcement?.SourceType
            };
        }
    }
}
