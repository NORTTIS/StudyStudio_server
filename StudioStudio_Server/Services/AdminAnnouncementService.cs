using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service x? l? business logic cho Announcements (admin side)
    /// </summary>
    public class AdminAnnouncementService : IAdminAnnouncementService
    {
        private readonly IAnnouncementRepository _announcementRepository;
        private readonly ILogger<AdminAnnouncementService> _logger;

        public AdminAnnouncementService(
            IAnnouncementRepository announcementRepository,
            ILogger<AdminAnnouncementService> logger)
        {
            _announcementRepository = announcementRepository;
            _logger = logger;
        }

        /// <summary>
        /// L?y t?t c? announcements (bao g?m c? inactive)
        /// </summary>
        public async Task<List<AnnouncementResponse>> GetAllAnnouncementsAsync()
        {
            var announcements = await _announcementRepository.GetAllAsync();

            return announcements
                .Select(MapToAnnouncementResponse)
                .ToList();
        }

        /// <summary>
        /// L?y chi ti?t m?t announcement
        /// Validate: Announcement ph?i t?n t?i
        /// </summary>
        public async Task<AnnouncementResponse> GetAnnouncementByIdAsync(Guid announcementId)
        {
            var announcement = await ValidateAnnouncementExistsAsync(announcementId);
            return MapToAnnouncementResponse(announcement);
        }

        /// <summary>
        /// T?o m?i announcement
        /// Logic: N?u IsActive = true và không có PublishedAt ? set PublishedAt = UtcNow
        /// </summary>
        public async Task<AnnouncementResponse> CreateAnnouncementAsync(
            Guid adminUserId, 
            CreateAnnouncementRequest request)
        {
            var now = DateTime.UtcNow;

            var announcement = new Announcement
            {
                AnnouncementId = Guid.NewGuid(),
                Title = request.Title,
                Content = request.Content,
                Type = request.Type,
                IsActive = request.IsActive,
                CreatedBy = adminUserId,
                CreatedAt = now,
                UpdatedAt = now,
                PublishedAt = DeterminePublishedAt(request.IsActive, null, request.PublishedAt)
            };

            await _announcementRepository.AddAsync(announcement);

            _logger.LogInformation(
                "Announcement created by admin {UserId}. AnnouncementId: {AnnouncementId}, Title: {Title}",
                adminUserId, announcement.AnnouncementId, announcement.Title);

            return MapToAnnouncementResponse(announcement);
        }

        /// <summary>
        /// C?p nh?t announcement
        /// Validate: Announcement ph?i t?n t?i
        /// Logic: X? l? PublishedAt d?a trên IsActive
        /// </summary>
        public async Task<AnnouncementResponse> UpdateAnnouncementAsync(
            Guid adminUserId, 
            UpdateAnnouncementRequest request)
        {
            var announcement = await ValidateAnnouncementExistsAsync(request.AnnouncementId);

            announcement.Title = request.Title;
            announcement.Content = request.Content;
            announcement.Type = request.Type;
            announcement.IsActive = request.IsActive;
            announcement.UpdatedAt = DateTime.UtcNow;
            announcement.PublishedAt = DeterminePublishedAt(
                request.IsActive,
                announcement.PublishedAt,
                request.PublishedAt);

            await _announcementRepository.UpdateAsync(announcement);

            _logger.LogInformation(
                "Announcement {AnnouncementId} updated by admin {UserId}. Title: {Title}",
                announcement.AnnouncementId, adminUserId, announcement.Title);

            return MapToAnnouncementResponse(announcement);
        }

        /// <summary>
        /// Xóa announcement
        /// Validate: Announcement ph?i t?n t?i
        /// </summary>
        public async Task DeleteAnnouncementAsync(Guid adminUserId, Guid announcementId)
        {
            var announcement = await ValidateAnnouncementExistsAsync(announcementId);

            await _announcementRepository.DeleteAsync(announcement);

            _logger.LogInformation(
                "Announcement {AnnouncementId} deleted by admin {UserId}. Title: {Title}",
                announcementId, adminUserId, announcement.Title);
        }

        /// <summary>
        /// Validate announcement có t?n t?i không
        /// Throw AppException n?u không t?m th?y
        /// </summary>
        private async Task<Announcement> ValidateAnnouncementExistsAsync(Guid announcementId)
        {
            var announcement = await _announcementRepository.GetByIdAsync(announcementId);

            if (announcement == null)
            {
                throw new AppException(
                    ErrorCodes.AnnouncementNotFound, 
                    StatusCodes.Status404NotFound);
            }

            return announcement;
        }

        /// <summary>
        /// Xác ð?nh giá tr? PublishedAt
        /// Logic:
        /// - N?u có requestPublishedAt ? dùng giá tr? ðó
        /// - N?u IsActive = true và chýa có PublishedAt ? set = UtcNow
        /// - Ngý?c l?i gi? nguyên giá tr? hi?n t?i
        /// </summary>
        private DateTime? DeterminePublishedAt(
            bool isActive, 
            DateTime? currentPublishedAt, 
            DateTime? requestPublishedAt)
        {
            if (requestPublishedAt.HasValue)
            {
                return requestPublishedAt;
            }

            if (isActive && !currentPublishedAt.HasValue)
            {
                return DateTime.UtcNow;
            }

            return currentPublishedAt;
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
    }
}
