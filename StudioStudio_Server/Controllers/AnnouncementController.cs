using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller for managing Announcements
    /// Route: /api/announcements
    /// </summary>
    [Route("api/announcements")]
    [ApiController]
    public class AnnouncementController(
        IAnnouncementService announcementService,
        IMessageService messageService) : ControllerBase
    {
        /// <summary>
        /// Authenticate and get userId from JWT token
        /// Validate: User must not be admin
        /// </summary>
        private Guid ValidateAndGetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(
                    ErrorCodes.AuthInvalidCredential,
                    StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null &&
                          bool.TryParse(isAdminClaim, out var adminResult) &&
                          adminResult;

            if (isAdmin)
            {
                throw new AppException(
                    ErrorCodes.AuthForbidden,
                    StatusCodes.Status403Forbidden);
            }

            return userId;
        }

        /// <summary>
        /// [PUBLIC] GET /api/announcements
        /// Get list of public announcements (IsActive = true)
        /// Order by: PublishedAt/CreatedAt DESC
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<ApiResponse<List<AnnouncementResponse>>>> GetAnnouncements()
        {
            var userId = ValidateAndGetUserId();
            var response = await announcementService.GetAllActiveAnnouncementsAsync(userId);
            var message = messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<List<AnnouncementResponse>>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [PUBLIC] GET /api/announcements/{id}
        /// Get details of an announcement with user-specific IsRead
        /// </summary>
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<AnnouncementResponse>>> GetAnnouncementById(Guid id)
        {
            var userId = ValidateAndGetUserId();
            var response = await announcementService.GetAnnouncementByIdAsync(id, userId);
            var message = messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<AnnouncementResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [AUTHORIZED] PUT /api/announcements/{announcementId}/read
        /// Mark announcement as read (works for both type 0-3 and type 4-17)
        /// </summary>
        [HttpPut("{announcementId}/read")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<object>>> MarkAnnouncementAsRead(
            Guid announcementId)
        {
            var userId = ValidateAndGetUserId();
            await announcementService.MarkAnnouncementAsReadAsync(announcementId, userId);
            var message = messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessGetData,
                message));
        }

        /// <summary>
        /// [AUTHORIZED] DELETE /api/announcements/{announcementId}
        /// Soft delete user announcement (IsDelete = true)
        /// </summary>
        [HttpDelete("{announcementId}")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<object>>> DeleteAnnouncement(
            Guid announcementId)
        {
            var userId = ValidateAndGetUserId();
            await announcementService.DeleteAnnouncementAsync(announcementId, userId);
            var message = messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessGetData,
                message));
        }
    }
}
