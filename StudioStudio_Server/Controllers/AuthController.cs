using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Models.DTOs;
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
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            await _authService.RegisterAsync(request);
            return Ok();
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest loginRequest)
        {
            string token = await _authService.LoginAsync(loginRequest, Response);
            return Ok(new
            {
                Token = token
            });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            string? refreshToken = Request.Cookies["refreshToken"];

            if (String.IsNullOrEmpty(refreshToken))
            {
                return Unauthorized("Missing refresh token");
            }

            var newAccessToken = await _authService.RefreshTokenAsync(refreshToken, Response);
            return Ok(new
            {
                AccessToken = newAccessToken,
            });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            string? refreshToken = Request.Cookies["refreshToken"];
            if (!String.IsNullOrEmpty(refreshToken))
            {
                await _authService.LogoutAsync(refreshToken, Response);
            }
            return Ok();
        }

        [HttpPost("google")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            var result = await _authService.GoogleLoginAsync(request, Response);
            return Ok(result);
        }

        [HttpPost("forgot")]
        public async Task<IActionResult> ForgotPassword([FromBody] string email)
        {
            await _authService.SendResetPasswordLinkAsync(email);
            return Ok();
        }

        [Authorize]
        [HttpPost("reset")]
        public async Task<IActionResult> ResetPassword()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            await _authService.SendResetPasswordLinkAsync(email);
            return Ok();
        }
    }
}
