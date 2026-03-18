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
        /// Get list of all active public announcements
        /// Condition: IsActive = true
        /// Order by: PublishedAt DESC
        /// CACHED: Uses AnnouncementExpiration (5 minutes)
        /// </summary>
        public async Task<List<AnnouncementResponse>> GetAllActiveAnnouncementsAsync(Guid userId)
        {
            var cacheKey = _cacheService.GetAnnouncementsKey();

            var announcements = await _cacheService.GetOrSetAsync(
                cacheKey,
                async () => await _announcementRepository.GetAllActiveAsync(userId),
                _cacheService.GetExpirationForKey(cacheKey)
            );

            return announcements?
                .Select(MapToAnnouncementResponse)
                .ToList() ?? new List<AnnouncementResponse>();
        }

        /// <summary>
        /// Get details of a specific public announcement
        /// Validate: Announcement must exist and be active (IsActive = true)
        /// </summary>
        public async Task<AnnouncementResponse> GetAnnouncementByIdAsync(Guid announcementId)
        {
            var announcement = await _announcementRepository.GetByIdAsync(announcementId);

            if (announcement == null || !announcement.IsActive)
            {
                throw new AppException(
                    ErrorCodes.AnnouncementNotFound,
                    StatusCodes.Status404NotFound);
            }

            return MapToAnnouncementResponse(announcement);
        }

        /// <summary>
        /// Get list of personalized announcements for user (tagged/mentioned announcements)
        /// Includes full announcement details joined with user announcement records
        /// Returns: User's personal announcement notifications
        /// </summary>
        public async Task<List<UserAnnouncementResponse>> GetUserAnnouncementsAsync(Guid userId)
        {
            var userAnnouncements = await _userAnnouncementRepository.GetByUserIdAsync(userId);

            var announcementIds = userAnnouncements
                .Select(ua => ua.AnnouncementId)
                .Distinct()
                .ToList();

            var announcementsDict = await GetAnnouncementsByIdsAsync(announcementIds);

            return userAnnouncements
                .Select(ua => MapToUserAnnouncementResponse(
                    ua,
                    announcementsDict.GetValueOrDefault(ua.AnnouncementId)))
                .ToList();
        }

        /// <summary>
        /// Mark announcement as read (unified endpoint for both user and public announcements)
        /// Case 1: If userAnnouncementId exists in UserAnnouncement -> Validate ownership and set IsRead = true
        /// Case 2: If not found in UserAnnouncement -> Treat as public announcementId and create new UserAnnouncement record
        /// This allows users to mark both personal and admin announcements as read using the same endpoint
        /// </summary>
        public async Task MarkAnnouncementAsReadAsync(Guid userAnnouncementId, Guid userId)
        {
            // Try to find existing UserAnnouncement first
            var userAnnouncement = await _userAnnouncementRepository.GetByIdAsync(userAnnouncementId);

            if (userAnnouncement != null)
            {
                // Case 1: Found in UserAnnouncement - update existing record
                ValidateUserAnnouncementOwnership(userAnnouncement, userId);

                if (!userAnnouncement.IsRead)
                {
                    userAnnouncement.IsRead = true;
                    await _userAnnouncementRepository.UpdateAsync(userAnnouncement);
                }
                return;
            }

            // Case 2: Not found in UserAnnouncement - treat as public announcement ID
            // Validate public announcement exists and is active
            var announcement = await _announcementRepository.GetByIdAsync(userAnnouncementId);

            if (announcement == null || !announcement.IsActive)
            {
                throw new AppException(
                    ErrorCodes.AnnouncementNotFound,
                    StatusCodes.Status404NotFound);
            }

            // Check if user already has this announcement marked
            var existingUserAnnouncements = await _userAnnouncementRepository.GetByUserIdAsync(userId);
            var existingRecord = existingUserAnnouncements
                .FirstOrDefault(ua => ua.AnnouncementId == userAnnouncementId);

            if (existingRecord != null)
            {
                // Already exists, just update IsRead = true
                if (!existingRecord.IsRead)
                {
                    existingRecord.IsRead = true;
                    await _userAnnouncementRepository.UpdateAsync(existingRecord);
                }
            }
            else
            {
                // Create new UserAnnouncement record for this public announcement
                var newUserAnnouncement = new UserAnnouncement
                {
                    UserAnnouncementId = Guid.NewGuid(),
                    AnnouncementId = userAnnouncementId,
                    MentionedId = userId,
                    CreatedBy = userId,
                    IsRead = true,
                    IsDelete = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _userAnnouncementRepository.AddAsync(newUserAnnouncement);
            }
        }

        /// <summary>
        /// Delete (soft delete) user announcement
        /// Validate: UserAnnouncement must exist and belong to the user
        /// Action: Set IsDelete = true in repository
        /// </summary>
        public async Task DeleteUserAnnouncementAsync(Guid userAnnouncementId, Guid userId)
        {
            var userAnnouncement = await _userAnnouncementRepository.GetByIdAsync(userAnnouncementId);

            if (userAnnouncement == null)
            {
                throw new AppException(
                    ErrorCodes.AnnouncementNotFound,
                    StatusCodes.Status404NotFound);
            }

            ValidateUserAnnouncementOwnership(userAnnouncement, userId);

            await _userAnnouncementRepository.DeleteAsync(userAnnouncementId);
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
        /// Map Announcement entity to AnnouncementResponse DTO
        /// </summary>
        private AnnouncementResponse MapToAnnouncementResponse(Announcement announcement)
        {
            return new AnnouncementResponse
            {
                AnnouncementId = announcement.AnnouncementId,
                Title = announcement.Title,
                Content = announcement.Content,
                Type = announcement.Type.ToString(),
                IsActive = announcement.IsActive,
                CreatedAt = announcement.CreatedAt,
                PublishedAt = announcement.PublishedAt
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
                CreatedBy = userAnnouncement.CreatedBy
            };
        }
    }
}
