using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    [Route("api/announcements")]
    [ApiController]
    public class AnnouncementController : ControllerBase
    {
        private readonly IAnnouncementRepository _announcementRepository;
        private readonly IUserAnnouccementRepository _userAnnouncementRepository;
        private readonly IMessageService _messageService;

        public AnnouncementController(
            IAnnouncementRepository announcementRepository,
            IUserAnnouccementRepository userAnnouncementRepository,
            IMessageService messageService)
        {
            _announcementRepository = announcementRepository;
            _userAnnouncementRepository = userAnnouncementRepository;
            _messageService = messageService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<AnnouncementResponse>>>> GetAnnouncements()
        {
            var announcements = await _announcementRepository.GetAllActiveAsync();

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
            var announcement = await _announcementRepository.GetByIdAsync(id);

            if (announcement == null || !announcement.IsActive)
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

        [HttpGet("user")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<List<UserAnnouncementResponse>>>> GetUserAnnouncements()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null && bool.TryParse(isAdminClaim, out var adminResult) && adminResult;
            if (isAdmin)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            var userAnnouncements = await _userAnnouncementRepository.GetByUserIdAsync(userId);

            var announcementIds = userAnnouncements.Select(ua => ua.AnnouncementId).ToList();
            var announcements = new List<Announcement>();
            
            foreach (var announcementId in announcementIds)
            {
                var announcement = await _announcementRepository.GetByIdAsync(announcementId);
                if (announcement != null)
                {
                    announcements.Add(announcement);
                }
            }

            var response = userAnnouncements.Select(ua =>
            {
                var announcement = announcements.FirstOrDefault(a => a.AnnouncementId == ua.AnnouncementId);
                return new UserAnnouncementResponse
                {
                    UserAnnouncementId = ua.UserAnnouncementId,
                    AnnouncementId = ua.AnnouncementId,
                    Title = announcement?.Title ?? "",
                    Content = announcement?.Content ?? "",
                    Type = announcement?.Type.ToString() ?? "",
                    IsRead = ua.IsRead,
                    CreatedAt = ua.CreatedAt,
                    PublishedAt = announcement?.PublishedAt
                };
            }).ToList();

            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);
            return Ok(ApiResponse<List<UserAnnouncementResponse>>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response
            ));
        }

        [HttpPut("user/{userAnnouncementId}/read")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<object>>> MarkAnnouncementAsRead(Guid userAnnouncementId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null && bool.TryParse(isAdminClaim, out var adminResult) && adminResult;
            if (isAdmin)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            var userAnnouncement = await _userAnnouncementRepository.GetByIdAsync(userAnnouncementId);
            if (userAnnouncement == null)
            {
                throw new AppException(ErrorCodes.AnnouncementNotFound, StatusCodes.Status404NotFound);
            }

            if (userAnnouncement.MetionedId != userId)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            userAnnouncement.IsRead = true;
            await _userAnnouncementRepository.UpdateAsync(userAnnouncement);

            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);
            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessGetData,
                message,
                null
            ));
        }

        [HttpDelete("user/{userAnnouncementId}")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<object>>> DeleteUserAnnouncement(Guid userAnnouncementId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null && bool.TryParse(isAdminClaim, out var adminResult) && adminResult;
            if (isAdmin)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            var userAnnouncement = await _userAnnouncementRepository.GetByIdAsync(userAnnouncementId);
            if (userAnnouncement == null)
            {
                throw new AppException(ErrorCodes.AnnouncementNotFound, StatusCodes.Status404NotFound);
            }

            if (userAnnouncement.MetionedId != userId)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            await _userAnnouncementRepository.DeleteAsync(userAnnouncementId);

            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);
            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessGetData,
                message,
                null
            ));
        }
    }
}
