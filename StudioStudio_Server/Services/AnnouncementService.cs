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
    /// </summary>
    public class AnnouncementService : IAnnouncementService
    {
        private readonly IAnnouncementRepository _announcementRepository;
        private readonly IUserAnnouccementRepository _userAnnouncementRepository;

        public AnnouncementService(
            IAnnouncementRepository announcementRepository,
            IUserAnnouccementRepository userAnnouncementRepository)
        {
            _announcementRepository = announcementRepository;
            _userAnnouncementRepository = userAnnouncementRepository;
        }

        /// <summary>
        /// Get list of all active public announcements
        /// Condition: IsActive = true
        /// Order by: PublishedAt DESC
        /// </summary>
        public async Task<List<AnnouncementResponse>> GetAllActiveAnnouncementsAsync()
        {
            var announcements = await _announcementRepository.GetAllActiveAsync();

            return announcements
                .Select(MapToAnnouncementResponse)
                .ToList();
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
        /// Mark user announcement as read
        /// Validate: UserAnnouncement must exist and belong to the user
        /// Action: Set IsRead = true
        /// </summary>
        public async Task MarkAnnouncementAsReadAsync(Guid userAnnouncementId, Guid userId)
        {
            var userAnnouncement = await _userAnnouncementRepository.GetByIdAsync(userAnnouncementId);

            if (userAnnouncement == null)
            {
                throw new AppException(
                    ErrorCodes.AnnouncementNotFound, 
                    StatusCodes.Status404NotFound);
            }

            ValidateUserAnnouncementOwnership(userAnnouncement, userId);

            userAnnouncement.IsRead = true;
            await _userAnnouncementRepository.UpdateAsync(userAnnouncement);
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
        /// Checks if MetionedId matches userId
        /// </summary>
        private void ValidateUserAnnouncementOwnership(UserAnnouncement userAnnouncement, Guid userId)
        {
            if (userAnnouncement.MetionedId != userId)
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
        /// </summary>
        private async Task<Dictionary<Guid, Announcement>> GetAnnouncementsByIdsAsync(List<Guid> announcementIds)
        {
            var announcements = new Dictionary<Guid, Announcement>();

            foreach (var announcementId in announcementIds)
            {
                var announcement = await _announcementRepository.GetByIdAsync(announcementId);
                if (announcement != null)
                {
                    announcements[announcementId] = announcement;
                }
            }

            return announcements;
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
                PublishedAt = announcement?.PublishedAt
            };
        }
    }
}
