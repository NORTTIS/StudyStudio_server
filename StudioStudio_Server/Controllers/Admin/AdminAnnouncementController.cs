using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers.Admin
{
    [Route("api/admin/announcements")]
    [ApiController]
    [Authorize]
    public class AdminAnnouncementController : ControllerBase
    {
        private readonly IAnnouncementRepository _announcementRepository;
        private readonly IMessageService _messageService;
        private readonly ILogger<AdminAnnouncementController> _logger;

        public AdminAnnouncementController(
            IAnnouncementRepository announcementRepository,
            IMessageService messageService,
            ILogger<AdminAnnouncementController> logger)
        {
            _announcementRepository = announcementRepository;
            _messageService = messageService;
            _logger = logger;
        }

        private Guid ValidateAdminUser()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null && bool.TryParse(isAdminClaim, out var adminResult) && adminResult;

            if (!isAdmin)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            return userId;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<AnnouncementResponse>>>> GetAllAnnouncements()
        {
            ValidateAdminUser();

            var announcements = await _announcementRepository.GetAllAsync();

            var response = announcements.Select(a => new AnnouncementResponse
            {
                AnnouncementId = a.AnnouncementId,
                Title = a.Title,
                Content = a.Content,
                Type = a.Type.ToString(),
                IsActive = a.IsActive,
                CreatedAt = a.CreatedAt,
                PublishedAt = a.PublishedAt
            }).ToList();

            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);
            return Ok(ApiResponse<List<AnnouncementResponse>>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response
            ));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<AnnouncementResponse>>> GetAnnouncementById(Guid id)
        {
            ValidateAdminUser();

            var announcement = await _announcementRepository.GetByIdAsync(id);
            if (announcement == null)
            {
                throw new AppException(ErrorCodes.AnnouncementNotFound, StatusCodes.Status404NotFound);
            }

            var response = new AnnouncementResponse
            {
                AnnouncementId = announcement.AnnouncementId,
                Title = announcement.Title,
                Content = announcement.Content,
                Type = announcement.Type.ToString(),
                IsActive = announcement.IsActive,
                CreatedAt = announcement.CreatedAt,
                PublishedAt = announcement.PublishedAt
            };

            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);
            return Ok(ApiResponse<AnnouncementResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response
            ));
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<AnnouncementResponse>>> CreateAnnouncement(
            [FromBody] CreateAnnouncementRequest request)
        {
            var userId = ValidateAdminUser();

            var now = DateTime.UtcNow;
            var announcement = new Announcement
            {
                AnnouncementId = Guid.NewGuid(),
                Title = request.Title,
                Content = request.Content,
                Type = request.Type,
                IsActive = request.IsActive,
                CreatedBy = userId,
                CreatedAt = now,
                UpdatedAt = now,
                PublishedAt = request.PublishedAt ?? (request.IsActive ? now : null)
            };

            await _announcementRepository.AddAsync(announcement);

            _logger.LogInformation(
                "Announcement created by admin {UserId}. AnnouncementId: {AnnouncementId}",
                userId, announcement.AnnouncementId);

            var response = new AnnouncementResponse
            {
                AnnouncementId = announcement.AnnouncementId,
                Title = announcement.Title,
                Content = announcement.Content,
                Type = announcement.Type.ToString(),
                IsActive = announcement.IsActive,
                CreatedAt = announcement.CreatedAt,
                PublishedAt = announcement.PublishedAt
            };

            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);
            return Ok(ApiResponse<AnnouncementResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response
            ));
        }

        [HttpPut]
        public async Task<ActionResult<ApiResponse<AnnouncementResponse>>> UpdateAnnouncement(
            [FromBody] UpdateAnnouncementRequest request)
        {
            var userId = ValidateAdminUser();

            var announcement = await _announcementRepository.GetByIdAsync(request.AnnouncementId);
            if (announcement == null)
            {
                throw new AppException(ErrorCodes.AnnouncementNotFound, StatusCodes.Status404NotFound);
            }

            announcement.Title = request.Title;
            announcement.Content = request.Content;
            announcement.Type = request.Type;
            announcement.IsActive = request.IsActive;
            announcement.UpdatedAt = DateTime.UtcNow;

            if (request.PublishedAt.HasValue)
            {
                announcement.PublishedAt = request.PublishedAt;
            }
            else if (request.IsActive && !announcement.PublishedAt.HasValue)
            {
                announcement.PublishedAt = DateTime.UtcNow;
            }

            await _announcementRepository.UpdateAsync(announcement);

            _logger.LogInformation(
                "Announcement {AnnouncementId} updated by admin {UserId}",
                announcement.AnnouncementId, userId);

            var response = new AnnouncementResponse
            {
                AnnouncementId = announcement.AnnouncementId,
                Title = announcement.Title,
                Content = announcement.Content,
                Type = announcement.Type.ToString(),
                IsActive = announcement.IsActive,
                CreatedAt = announcement.CreatedAt,
                PublishedAt = announcement.PublishedAt
            };

            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);
            return Ok(ApiResponse<AnnouncementResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response
            ));
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteAnnouncement(Guid id)
        {
            var userId = ValidateAdminUser();

            var announcement = await _announcementRepository.GetByIdAsync(id);
            if (announcement == null)
            {
                throw new AppException(ErrorCodes.AnnouncementNotFound, StatusCodes.Status404NotFound);
            }

            await _announcementRepository.DeleteAsync(announcement);

            _logger.LogInformation(
                "Announcement {AnnouncementId} deleted by admin {UserId}",
                id, userId);

            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);
            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessGetData,
                message,
                null
            ));
        }
    }
}
