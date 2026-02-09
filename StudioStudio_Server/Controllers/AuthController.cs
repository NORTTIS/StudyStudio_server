using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequests request)
        {
            await _authService.RegisterAsync(request);
            return Ok();
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
            var token = await _authService.LoginAsync(request, Response);
            return Ok(new { accessToken = token });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken()
        {
            if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken)
                || string.IsNullOrWhiteSpace(refreshToken))
            {
                return Unauthorized(new { message = "Refresh token not found" });
            }

            var accessToken = await _authService.RefreshTokenAsync(refreshToken, Response);

            return Ok(new { accessToken });
        }


        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var refreshToken = Request.Cookies["refreshToken"];
            await _authService.LogoutAsync(refreshToken!, Response);
            return Ok();
        }

        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            var accessToken = await _authService.GoogleLoginAsync(request, Response);

            return Ok(new
            {
                accessToken
            });
        }

        [HttpPost("resend-email")]
        public async Task<IActionResult> ResendEmail([FromBody] ResendEmailRequest request)
        {
            await _authService.ResendEmailAsync(request);
            return Ok(new
            {
                message = "Email đã được gửi"
            });
        }
    }
}
