using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers.Admin
{
    /// <summary>
    /// Admin Controller for managing Announcements
    /// Route: /api/admin/announcements
    /// </summary>
    [Route("api/admin/announcements")]
    [ApiController]
    [Authorize]
    public class AdminAnnouncementController(
        IAdminAnnouncementService adminAnnouncementService,
        IMessageService messageService) : ControllerBase
    {
        /// <summary>
        /// Validate user is admin
        /// Throw 403 if not admin
        /// </summary>
        private Guid ValidateAdminUser()
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

            if (!isAdmin)
            {
                throw new AppException(
                    ErrorCodes.AuthForbidden, 
                    StatusCodes.Status403Forbidden);
            }

            return userId;
        }

        /// <summary>
        /// [ADMIN] GET /api/admin/announcements
        /// Get all announcements (including inactive)
        /// Order by: CreatedAt DESC
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<AnnouncementResponse>>>> GetAllAnnouncements()
        {
            ValidateAdminUser();

            var response = await adminAnnouncementService.GetAllAnnouncementsAsync();
            var message = messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<List<AnnouncementResponse>>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] GET /api/admin/announcements/{id}
        /// Get announcement details
        /// Validate: Announcement must exist
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<AnnouncementResponse>>> GetAnnouncementById(Guid id)
        {
            ValidateAdminUser();

            var response = await adminAnnouncementService.GetAnnouncementByIdAsync(id);
            var message = messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<AnnouncementResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] POST /api/admin/announcements
        /// Create new announcement
        /// Auto-set: PublishedAt if IsActive = true
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<AnnouncementResponse>>> CreateAnnouncement(
            [FromBody] CreateAnnouncementRequest request)
        {
            var userId = ValidateAdminUser();

            var response = await adminAnnouncementService.CreateAnnouncementAsync(userId, request);
            var message = messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<AnnouncementResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] PUT /api/admin/announcements
        /// Update announcement
        /// Validate: Announcement must exist
        /// </summary>
        [HttpPut]
        public async Task<ActionResult<ApiResponse<AnnouncementResponse>>> UpdateAnnouncement(
            [FromBody] UpdateAnnouncementRequest request)
        {
            var userId = ValidateAdminUser();

            var response = await adminAnnouncementService.UpdateAnnouncementAsync(userId, request);
            var message = messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<AnnouncementResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] DELETE /api/admin/announcements/{id}
        /// Delete announcement
        /// Validate: Announcement must exist
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteAnnouncement(Guid id)
        {
            var userId = ValidateAdminUser();

            await adminAnnouncementService.DeleteAnnouncementAsync(userId, id);
            var message = messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessGetData,
                message));
        }
    }
}
