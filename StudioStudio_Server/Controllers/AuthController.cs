using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IMessageService _messageService;

        public AuthController(IAuthService authService, IMessageService messageService)
        {
            _authService = authService;
            _messageService = messageService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequests request)
        {
            await _authService.RegisterAsync(request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessRegister);
            return Ok(ApiResponse<object>.Success(ErrorCodes.SuccessRegister, message));
        }

        [HttpGet("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new AppException(ErrorCodes.ValidationRequiredField, StatusCodes.Status400BadRequest);
            }

            await _authService.VerifyEmailLinkAsync(token);
            var message = _messageService.GetMessage(ErrorCodes.SuccessVerifyEmail);
            return Ok(ApiResponse<object>.Success(ErrorCodes.SuccessVerifyEmail, message));
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequests loginRequest)
        {
            var loginResponse = await _authService.LoginAsync(loginRequest, Response);
            var message = _messageService.GetMessage(ErrorCodes.SuccessLogin);
            return Ok(ApiResponse<LoginResponse>.Success(ErrorCodes.SuccessLogin, message, loginResponse));
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            string? refreshToken = Request.Cookies["refreshToken"];

            if (string.IsNullOrEmpty(refreshToken))
            {
                throw new AppException(ErrorCodes.AuthTokenExpired, StatusCodes.Status401Unauthorized);
            }

            var refreshResponse = await _authService.RefreshTokenAsync(refreshToken, Response);
            var message = _messageService.GetMessage(ErrorCodes.SuccessRefreshToken);
            return Ok(ApiResponse<LoginResponse>.Success(ErrorCodes.SuccessRefreshToken, message, refreshResponse));
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            string? refreshToken = Request.Cookies["refreshToken"];
            if (!string.IsNullOrEmpty(refreshToken))
            {
                await _authService.LogoutAsync(refreshToken, Response);
            }
            var message = _messageService.GetMessage(ErrorCodes.SuccessLogout);
            return Ok(ApiResponse<object>.Success(ErrorCodes.SuccessLogout, message));
        }

        [HttpPost("google")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            var loginResponse = await _authService.GoogleLoginAsync(request, Response);
            var message = _messageService.GetMessage(ErrorCodes.SuccessLogin);
            return Ok(ApiResponse<LoginResponse>.Success(ErrorCodes.SuccessLogin, message, loginResponse));
        }

        [HttpPost("forgot")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            await _authService.SendResetPasswordLinkAsync(request.Email);
            var message = _messageService.GetMessage(ErrorCodes.SuccessResetPassword);
            return Ok(ApiResponse<object>.Success(ErrorCodes.SuccessResetPassword, message));
        }

        [Authorize]
        [HttpPost("reset")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            await _authService.ResetPasswordAsync(request.Token, request.NewPassword);
            var message = _messageService.GetMessage(ErrorCodes.SuccessResetPassword);
            return Ok(ApiResponse<object>.Success(ErrorCodes.SuccessResetPassword, message));
        }

        [HttpGet("verify-reset-token")]
        public async Task<IActionResult> VerifyResetToken([FromQuery] string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new AppException(ErrorCodes.ValidationRequiredField, StatusCodes.Status400BadRequest);
            }

            var isValid = await _authService.VerifyResetTokenAsync(token);

            if (!isValid)
            {
                throw new AppException(ErrorCodes.ValidationInvalidToken, StatusCodes.Status400BadRequest);
            }
            var message = _messageService.GetMessage(ErrorCodes.SuccessVerifyEmail);
            return Ok(ApiResponse<object>.Success(ErrorCodes.SuccessVerifyEmail, message));
        }
    }
}
