using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller qu?n l? announcements cho Admin
    /// Route: /api/admin/announcements
    /// </summary>
    [Route("api/admin/announcements")]
    [ApiController]
    [Authorize]
    public class AdminAnnouncementController : ControllerBase
    {
        private readonly IAdminAnnouncementService _adminAnnouncementService;
        private readonly IMessageService _messageService;

        public AdminAnnouncementController(
            IAdminAnnouncementService adminAnnouncementService,
            IMessageService messageService)
        {
            _adminAnnouncementService = adminAnnouncementService;
            _messageService = messageService;
        }

        /// <summary>
        /// Xác th?c user là admin và l?y userId
        /// Validate: User ph?i có IsAdmin = true
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
        /// L?y t?t c? announcements (bao g?m inactive)
        /// S?p x?p: CreatedAt DESC
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<AnnouncementResponse>>>> GetAllAnnouncements()
        {
            ValidateAdminUser();

            var response = await _adminAnnouncementService.GetAllAnnouncementsAsync();
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<List<AnnouncementResponse>>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] GET /api/admin/announcements/{id}
        /// L?y chi ti?t m?t announcement
        /// Validate: Announcement ph?i t?n t?i
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<AnnouncementResponse>>> GetAnnouncementById(Guid id)
        {
            ValidateAdminUser();

            var response = await _adminAnnouncementService.GetAnnouncementByIdAsync(id);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<AnnouncementResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] POST /api/admin/announcements
        /// T?o m?i announcement
        /// Auto-set: PublishedAt n?u IsActive = true
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<AnnouncementResponse>>> CreateAnnouncement(
            [FromBody] CreateAnnouncementRequest request)
        {
            var userId = ValidateAdminUser();

            var response = await _adminAnnouncementService.CreateAnnouncementAsync(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<AnnouncementResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] PUT /api/admin/announcements
        /// C?p nh?t announcement
        /// Validate: Announcement ph?i t?n t?i
        /// </summary>
        [HttpPut]
        public async Task<ActionResult<ApiResponse<AnnouncementResponse>>> UpdateAnnouncement(
            [FromBody] UpdateAnnouncementRequest request)
        {
            var userId = ValidateAdminUser();

            var response = await _adminAnnouncementService.UpdateAnnouncementAsync(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<AnnouncementResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] DELETE /api/admin/announcements/{id}
        /// Xóa announcement
        /// Validate: Announcement ph?i t?n t?i
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteAnnouncement(Guid id)
        {
            var userId = ValidateAdminUser();

            await _adminAnnouncementService.DeleteAnnouncementAsync(userId, id);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessGetData,
                message,
                null));
        }
    }
}
