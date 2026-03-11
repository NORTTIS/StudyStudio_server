using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service handling business logic for Announcements (admin side)
    /// Manages CRUD operations for system-wide announcements
    /// Only admins can create, update, and delete announcements
    /// CACHE INVALIDATION: Automatically invalidates announcement caches when data changes
    /// </summary>
    public class AdminAnnouncementService : IAdminAnnouncementService
    {
        private readonly IAnnouncementRepository _announcementRepository;
        private readonly ILogger<AdminAnnouncementService> _logger;
        private readonly ICacheService _cacheService;

        public AdminAnnouncementService(
            IAnnouncementRepository announcementRepository,
            ILogger<AdminAnnouncementService> logger,
            ICacheService cacheService)
        {
            _announcementRepository = announcementRepository;
            _logger = logger;
            _cacheService = cacheService;
        }

        /// <summary>
        /// Get all announcements (including inactive ones)
        /// Returns: List of all announcements regardless of IsActive status
        /// Use case: Admin dashboard to manage all announcements
        /// </summary>
        public async Task<List<AnnouncementResponse>> GetAllAnnouncementsAsync()
        {
            var announcements = await _announcementRepository.GetAllAsync();

            return announcements
                .Select(MapToAnnouncementResponse)
                .ToList();
        }

        /// <summary>
        /// Get announcement details by ID
        /// Validate: Announcement must exist
        /// Returns: Complete announcement information
        /// </summary>
        public async Task<AnnouncementResponse> GetAnnouncementByIdAsync(Guid announcementId)
        {
            var announcement = await ValidateAnnouncementExistsAsync(announcementId);
            return MapToAnnouncementResponse(announcement);
        }

        /// <summary>
        /// Create new announcement
        /// Logic: If IsActive = true and PublishedAt not provided, set PublishedAt = UtcNow
        /// CreatedBy: Admin user ID
        /// CACHE: Invalidates announcement cache after creation
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

            // Invalidate announcement cache
            await _cacheService.InvalidateAnnouncementCachesAsync();

            _logger.LogInformation(
                "Announcement created by admin {UserId}. AnnouncementId: {AnnouncementId}, Title: {Title}",
                adminUserId, announcement.AnnouncementId, announcement.Title);

            return MapToAnnouncementResponse(announcement);
        }

        /// <summary>
        /// Update announcement
        /// Validate: Announcement must exist
        /// Logic: Handle PublishedAt based on IsActive status
        /// UpdatedAt: Auto-set to UtcNow
        /// CACHE: Invalidates announcement cache after update
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

            // Invalidate announcement cache
            await _cacheService.InvalidateAnnouncementCachesAsync();

            _logger.LogInformation(
                "Announcement {AnnouncementId} updated by admin {UserId}. Title: {Title}",
                announcement.AnnouncementId, adminUserId, announcement.Title);

            return MapToAnnouncementResponse(announcement);
        }

        /// <summary>
        /// Delete announcement (hard delete)
        /// Validate: Announcement must exist
        /// Note: This is a permanent deletion, not soft delete
        /// CACHE: Invalidates announcement cache after deletion
        /// </summary>
        public async Task DeleteAnnouncementAsync(Guid adminUserId, Guid announcementId)
        {
            var announcement = await ValidateAnnouncementExistsAsync(announcementId);

            await _announcementRepository.DeleteAsync(announcement);

            // Invalidate announcement cache
            await _cacheService.InvalidateAnnouncementCachesAsync();

            _logger.LogInformation(
                "Announcement {AnnouncementId} deleted by admin {UserId}. Title: {Title}",
                announcementId, adminUserId, announcement.Title);
        }

        /// <summary>
        /// Validate announcement exists
        /// Throws AppException if not found
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
        /// Determine PublishedAt value based on IsActive status
        /// Logic:
        /// - If requestPublishedAt is provided, use it
        /// - If IsActive = true and no existing PublishedAt, set to UtcNow
        /// - Otherwise, keep current value
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
    }
}
