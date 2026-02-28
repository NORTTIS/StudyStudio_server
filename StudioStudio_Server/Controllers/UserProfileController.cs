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
    /// Controller quản lý User Profile (thông tin cá nhân)
    /// Route: /api
    /// </summary>
    [Route("api")]
    [ApiController]
    [Authorize]
    public class UserProfileController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IMessageService _messageService;

        public UserProfileController(IUserService userService, IMessageService messageService)
        {
            _userService = userService;
            _messageService = messageService;
        }

        /// <summary>
        /// Xác thực và lấy userId từ JWT token
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

            return userId;
        }

        /// <summary>
        /// Build absolute URL cho avatar từ relative path
        /// Convert: /uploads/avatars/xxx.jpg → https://domain.com/uploads/avatars/xxx.jpg
        /// </summary>
        private string? BuildAbsoluteAvatarUrl(string? avatarUrl)
        {
            if (!string.IsNullOrEmpty(avatarUrl) && avatarUrl.StartsWith("/"))
            {
                var request = HttpContext.Request;
                return $"{request.Scheme}://{request.Host}{avatarUrl}";
            }

            return avatarUrl;
        }

        /// <summary>
        /// [AUTHORIZED] GET /api/user-profile
        /// Lấy thông tin profile của user hiện tại
        /// Include: Avatar URL (absolute), settings, Google OAuth status
        /// </summary>
        [HttpGet("user-profile")]
        public async Task<ActionResult<ApiResponse<UserProfileResponse>>> GetUserProfile()
        {
            var userId = ValidateAndGetUserId();
            var user = await _userService.GetByIdAsync(userId);

            if (user == null)
            {
                throw new AppException(
                    ErrorCodes.UserNotFound,
                    StatusCodes.Status404NotFound);
            }

            var response = new UserProfileResponse
            {
                UserId = user.UserId,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                Bio = user.Bio,
                AvatarUrl = BuildAbsoluteAvatarUrl(user.AvatarUrl),
                Status = user.Status.ToString(),
                IsAdmin = user.IsAdmin,
                Language = user.Language,
                EmailNotificationEnabled = user.EmailNotificationEnabled,
                GoogleId = user.GoogleId,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };

            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<UserProfileResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [AUTHORIZED] PUT /api/user-profile
        /// Cập nhật thông tin profile
        /// Support: FirstName, LastName, PhoneNumber, Bio, Language, EmailNotificationEnabled
        /// Avatar upload: Multipart form-data với file
        /// </summary>
        [HttpPut("user-profile")]
        public async Task<IActionResult> UpdateUserProfile([FromForm] UpdateUserProfileRequest request)
        {
            var userId = ValidateAndGetUserId();
            await _userService.UpdateProfileAsync(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessUpdateProfile);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessUpdateProfile,
                message,
                null));
        }

        /// <summary>
        /// [AUTHORIZED] POST /api/change-password
        /// Đổi password (cho accounts không dùng Google OAuth)
        /// Validate:
        /// - CurrentPassword phải đúng
        /// - NewPassword đủ mạnh
        /// - NewPassword khác CurrentPassword
        /// </summary>
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userId = ValidateAndGetUserId();
            await _userService.ChangePasswordAsync(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessChangePassword);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessChangePassword,
                message,
                null));
        }

        /// <summary>
        /// [AUTHORIZED] DELETE /api/user-profile
        /// Xóa tài khoản (soft delete)
        /// Action: Set DeletedFlag = true
        /// Effect: User không thể login lại
        /// </summary>
        [HttpDelete("user-profile")]
        public async Task<IActionResult> DeleteAccount()
        {
            var userId = ValidateAndGetUserId();
            await _userService.DeleteAsync(userId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessDeleteAccount);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessDeleteAccount,
                message,
                null));
        }
    }
}