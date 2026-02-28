using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service x? l? business logic cho Announcements (user side)
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
        /// L?y danh sách t?t c? announcements công khai (active)
        /// </summary>
        public async Task<List<AnnouncementResponse>> GetAllActiveAnnouncementsAsync()
        {
            var announcements = await _announcementRepository.GetAllActiveAsync();

            return announcements
                .Select(MapToAnnouncementResponse)
                .ToList();
        }

        /// <summary>
        /// L?y chi ti?t m?t announcement công khai
        /// Validate: Announcement ph?i t?n t?i và IsActive = true
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
        /// L?y danh sách announcements cá nhân c?a user (nh?ng announcement user ðý?c tag)
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
        /// Ðánh d?u announcement cá nhân là ð? ð?c
        /// Validate: UserAnnouncement ph?i t?n t?i và thu?c v? user
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
        /// Xóa (soft delete) announcement cá nhân
        /// Validate: UserAnnouncement ph?i t?n t?i và thu?c v? user
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
        /// Validate user có quy?n s? h?u UserAnnouncement không
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
        /// L?y danh sách announcements theo list IDs
        /// Return: Dictionary v?i key = AnnouncementId
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
        /// Map Announcement entity ? AnnouncementResponse DTO
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
        /// Map UserAnnouncement + Announcement ? UserAnnouncementResponse DTO
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
