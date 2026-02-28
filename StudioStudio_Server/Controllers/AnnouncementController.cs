using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller quản lý announcements cho users (không phải admin)
    /// Route: /api/announcements
    /// </summary>
    [Route("api/announcements")]
    [ApiController]
    public class AnnouncementController : ControllerBase
    {
        private readonly IAnnouncementService _announcementService;
        private readonly IMessageService _messageService;

        public AnnouncementController(
            IAnnouncementService announcementService,
            IMessageService messageService)
        {
            _announcementService = announcementService;
            _messageService = messageService;
        }

        /// <summary>
        /// Xác thực và lấy userId từ JWT token
        /// Validate: User không được là admin
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
        /// Lấy danh sách announcements công khai (IsActive = true)
        /// Sắp xếp: PublishedAt/CreatedAt DESC
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<AnnouncementResponse>>>> GetAnnouncements()
        {
            var response = await _announcementService.GetAllActiveAnnouncementsAsync();
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<List<AnnouncementResponse>>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [PUBLIC] GET /api/announcements/{id}
        /// Lấy chi tiết một announcement công khai
        /// Validate: Announcement phải active
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<AnnouncementResponse>>> GetAnnouncementById(Guid id)
        {
            var response = await _announcementService.GetAnnouncementByIdAsync(id);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<AnnouncementResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [AUTHORIZED] GET /api/announcements/user
        /// Lấy danh sách announcements cá nhân của user (được mention/tag)
        /// Sắp xếp: CreatedAt DESC
        /// </summary>
        [HttpGet("user")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<List<UserAnnouncementResponse>>>> GetUserAnnouncements()
        {
            var userId = ValidateAndGetUserId();
            var response = await _announcementService.GetUserAnnouncementsAsync(userId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<List<UserAnnouncementResponse>>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [AUTHORIZED] PUT /api/announcements/user/{userAnnouncementId}/read
        /// Đánh dấu announcement cá nhân là đã đọc (IsRead = true)
        /// Validate: User phải sở hữu announcement này
        /// </summary>
        [HttpPut("user/{userAnnouncementId}/read")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<object>>> MarkAnnouncementAsRead(
            Guid userAnnouncementId)
        {
            var userId = ValidateAndGetUserId();
            await _announcementService.MarkAnnouncementAsReadAsync(userAnnouncementId, userId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessGetData,
                message,
                null));
        }

        /// <summary>
        /// [AUTHORIZED] DELETE /api/announcements/user/{userAnnouncementId}
        /// Xóa (soft delete) announcement cá nhân (IsDelete = true)
        /// Validate: User phải sở hữu announcement này
        /// </summary>
        [HttpDelete("user/{userAnnouncementId}")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<object>>> DeleteUserAnnouncement(
            Guid userAnnouncementId)
        {
            var userId = ValidateAndGetUserId();
            await _announcementService.DeleteUserAnnouncementAsync(userAnnouncementId, userId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessGetData,
                message,
                null));
        }
    }
}
