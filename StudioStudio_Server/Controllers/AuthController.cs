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
            return Ok(ApiResponse<object>.Success(message));
        }

        [HttpGet("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status400BadRequest);
            }

            await _authService.VerifyEmailLinkAsync(token);
            var message = _messageService.GetMessage(ErrorCodes.SuccessVerifyEmail);
            return Ok(ApiResponse<object>.Success(message));
        }

        [HttpGet("verify-email")]
        public async Task<IActionResult> VerifyEmail(string token)
        {
            await _authService.VerifyEmailAsync(token);
            return Ok();
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequests request)
        {
            var loginResponse = await _authService.LoginAsync(loginRequest, Response);
            var message = _messageService.GetMessage(ErrorCodes.SuccessLogin);
            return Ok(ApiResponse<LoginResponse>.Success(message, loginResponse));
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken()
        {
            string? refreshToken = Request.Cookies["refreshToken"];

            if (string.IsNullOrEmpty(refreshToken))
            {
                throw new AppException(ErrorCodes.AuthTokenExpired, StatusCodes.Status401Unauthorized);
            }

            var refreshResponse = await _authService.RefreshTokenAsync(refreshToken, Response);
            var message = _messageService.GetMessage(ErrorCodes.SuccessRefreshToken);
            return Ok(ApiResponse<LoginResponse>.Success(message, refreshResponse));
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
            return Ok(ApiResponse<object>.Success(message));
        }

        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            var loginResponse = await _authService.GoogleLoginAsync(request, Response);
            var message = _messageService.GetMessage(ErrorCodes.SuccessLogin);
            return Ok(ApiResponse<LoginResponse>.Success(message, loginResponse));
        }

        [HttpPost("forgot")]
        public async Task<IActionResult> ForgotPassword([FromBody] string email)
        {
            await _authService.SendResetPasswordLinkAsync(email);
            return Ok(ApiResponse<object>.Success("Password reset email sent successfully"));
        }

        [HttpPost("resend-email")]
        public async Task<IActionResult> ResendEmail([FromBody] ResendEmailRequest request)
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email))
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }
            
            await _authService.SendResetPasswordLinkAsync(email);
            return Ok(ApiResponse<object>.Success("Password reset email sent successfully"));
        }
    }
}
