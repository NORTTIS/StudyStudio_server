using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
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

        [HttpGet("user-profile")]
        public async Task<IActionResult> GetUserProfile()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var user = await _userService.GetByIdAsync(userId);
            
            if (user == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }

            // Build absolute URL for avatar if it's a relative path
            string? avatarUrl = user.AvatarUrl;
            if (!string.IsNullOrEmpty(avatarUrl) && avatarUrl.StartsWith("/"))
            {
                var request = HttpContext.Request;
                avatarUrl = $"{request.Scheme}://{request.Host}{avatarUrl}";
            }

            var response = new UserProfileResponse
            {
                UserId = user.UserId,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                Bio = user.Bio,
                AvatarUrl = avatarUrl,
                Status = user.Status.ToString(),
                IsAdmin = user.IsAdmin,
                Language = user.Language,
                EmailNotificationEnabled = user.EmailNotificationEnabled,
                GoogleId = user.GoogleId,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };

            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);
            return Ok(ApiResponse<UserProfileResponse>.Success(ErrorCodes.SuccessGetData, message, response));
        }

        [HttpPut("user-profile")]
        public async Task<IActionResult> UpdateUserProfile([FromForm] UpdateUserProfileRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            await _userService.UpdateProfileAsync(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessUpdateProfile);
            return Ok(ApiResponse<object>.Success(ErrorCodes.SuccessUpdateProfile, message));
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            await _userService.ChangePasswordAsync(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessChangePassword);
            return Ok(ApiResponse<object>.Success(ErrorCodes.SuccessChangePassword, message));
        }
    }
}