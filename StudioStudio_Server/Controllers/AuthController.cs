using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller for Authentication
    /// Route: /api/auth
    /// Includes: Register, Login, Google OAuth, Email verification, Password reset
    /// </summary>
    [Route("api/auth")]
    [ApiController]
    public class AuthController(IAuthService authService, IMessageService messageService) : ControllerBase
    {
        /// <summary>
        /// [PUBLIC] POST /api/auth/register
        /// Register new user account
        /// Validate: Email format, Password strength
        /// Action: Create user with Status = Pending, Send verification email
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequests request)
        {
            await authService.RegisterAsync(request);
            var message = messageService.GetMessage(ErrorCodes.SuccessRegister);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessRegister,
                message));
        }

        /// <summary>
        /// [PUBLIC] POST /api/auth/login
        /// Login with email and password
        /// Validate: Email verified, Password correct
        /// Return: JWT AccessToken + RefreshToken (HTTP-only cookie)
        /// </summary>
        [HttpPost("login")]
        public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequests request)
        {
            var loginResponse = await authService.LoginAsync(request, Response);
            var message = messageService.GetMessage(ErrorCodes.SuccessLogin);

            return Ok(ApiResponse<LoginResponse>.Success(
                ErrorCodes.SuccessLogin,
                message,
                loginResponse));
        }

        /// <summary>
        /// [PUBLIC] POST /api/auth/google
        /// Login with Google OAuth
        /// Validate: Google ID Token
        /// Action: Create user if not exists, Return JWT + RefreshToken
        /// </summary>
        [HttpPost("google")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            var loginResponse = await authService.GoogleLoginAsync(request, Response);
            var message = messageService.GetMessage(ErrorCodes.SuccessLogin);

            return Ok(ApiResponse<LoginResponse>.Success(
                ErrorCodes.SuccessLogin,
                message,
                loginResponse));
        }

        /// <summary>
        /// [PUBLIC] POST /api/auth/refresh
        /// Refresh access token using refresh token
        /// Validate: RefreshToken exists and not expired
        /// Return: New JWT AccessToken + new RefreshToken
        /// </summary>
        [HttpPost("refresh")]
        public async Task<ActionResult<ApiResponse<LoginResponse>>> Refresh([FromBody] RefreshTokenRequest request)
        {
            var refreshResponse = await authService.RefreshTokenAsync(request.RefreshToken, Response);
            var message = messageService.GetMessage(ErrorCodes.SuccessRefreshToken);

            return Ok(ApiResponse<LoginResponse>.Success(
                ErrorCodes.SuccessRefreshToken,
                message,
                refreshResponse));
        }

        /// <summary>
        /// [PUBLIC] POST /api/auth/logout
        /// Logout and invalidate refresh token
        /// Action: Delete RefreshToken from database and clear cookie
        /// </summary>
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            string? refreshToken = Request.Cookies["refreshToken"];

            if (!string.IsNullOrEmpty(refreshToken))
            {
                await authService.LogoutAsync(refreshToken, Response);
            }

            var message = messageService.GetMessage(ErrorCodes.SuccessLogout);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessLogout,
                message));
        }

        /// <summary>
        /// [PUBLIC] GET /api/auth/verify-email?token={token}
        /// Verify email with token from email link
        /// Validate: Token exists and not expired (30 minutes)
        /// Action: Set User Status = Active
        /// </summary>
        [HttpGet("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new AppException(
                    ErrorCodes.ValidationRequiredField);
            }

            await authService.VerifyEmailLinkAsync(token);
            var message = messageService.GetMessage(ErrorCodes.SuccessVerifyEmail);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessVerifyEmail,
                message));
        }

        /// <summary>
        /// [PUBLIC] POST /api/auth/resend-email-verify
        /// Resend verification email
        /// Validate:
        /// - Email exists
        /// - Email not yet verified
        /// Action: Send new email with new token (expire 30 minutes)
        /// </summary>
        [HttpPost("resend-email-verify")]
        public async Task<IActionResult> ResendEmailVerify([FromBody] ResendVerifyEmailRequest request)
        {
            await authService.ResendVerifyEmailAsync(request);
            var message = messageService.GetMessage(ErrorCodes.SuccessResendEmailVerify);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessResendEmailVerify,
                message));
        }

        /// <summary>
        /// [PUBLIC] POST /api/auth/forgot
        /// Send password reset link via email
        /// Validate: Email exists
        /// Action: Send email with reset token (expire 15 minutes, stored in Redis)
        /// </summary>
        [HttpPost("forgot")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            await authService.SendResetPasswordLinkAsync(request.Email);
            var message = messageService.GetMessage(ErrorCodes.SuccessSendForgotLink);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessSendForgotLink,
                message));
        }

        /// <summary>
        /// [PUBLIC] GET /api/auth/verify-reset-token?token={token}
        /// Check if password reset token is valid
        /// Validate: Token exists in Redis and not expired
        /// Use case: Frontend validates token before showing reset password form
        /// </summary>
        [HttpGet("verify-reset-token")]
        public async Task<IActionResult> VerifyResetToken([FromQuery] string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new AppException(
                    ErrorCodes.ValidationRequiredField);
            }

            var isValid = await authService.VerifyResetTokenAsync(token);

            if (!isValid)
            {
                throw new AppException(
                    ErrorCodes.ValidationInvalidToken);
            }

            var message = messageService.GetMessage(ErrorCodes.SuccessVerifyEmail);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessVerifyEmail,
                message));
        }

        /// <summary>
        /// [PUBLIC] POST /api/auth/reset-password
        /// Reset password with token from email
        /// Validate:
        /// - Token is valid and not expired
        /// - New password is strong enough
        /// Action: Update password and delete token from Redis
        /// </summary>
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            await authService.ResetPasswordAsync(request.Token, request.NewPassword);
            var message = messageService.GetMessage(ErrorCodes.SuccessResetPassword);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessResetPassword,
                message));
        }
    }
}
