using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller xử lý Authentication và Authorization
    /// Route: /api/auth
    /// Bao gồm: Register, Login, Logout, Email verification, Password reset, Google OAuth
    /// </summary>
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

        /// <summary>
        /// Lấy refresh token từ HTTP cookie
        /// </summary>
        private string GetRefreshTokenFromCookie()
        {
            string? refreshToken = Request.Cookies["refreshToken"];

            if (string.IsNullOrEmpty(refreshToken))
            {
                throw new AppException(
                    ErrorCodes.AuthTokenExpired,
                    StatusCodes.Status401Unauthorized);
            }

            return refreshToken;
        }

        /// <summary>
        /// [PUBLIC] POST /api/auth/register
        /// Đăng ký tài khoản mới
        /// Validate:
        /// - Email chưa tồn tại
        /// - Password đủ mạnh
        /// - ConfirmPassword khớp với Password
        /// Action: Gửi email xác thực
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequests request)
        {
            await _authService.RegisterAsync(request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessRegister);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessRegister,
                message,
                null));
        }

        /// <summary>
        /// [PUBLIC] POST /api/auth/login
        /// Đăng nhập với email và password
        /// Validate:
        /// - Email tồn tại
        /// - Password đúng
        /// - Email đã được xác thực
        /// Return: AccessToken (JWT) + RefreshToken (HTTP-only cookie)
        /// </summary>
        [HttpPost("login")]
        public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequests request)
        {
            var loginResponse = await _authService.LoginAsync(request, Response);
            var message = _messageService.GetMessage(ErrorCodes.SuccessLogin);

            return Ok(ApiResponse<LoginResponse>.Success(
                ErrorCodes.SuccessLogin,
                message,
                loginResponse));
        }

        /// <summary>
        /// [PUBLIC] POST /api/auth/google
        /// Đăng nhập/Đăng ký với Google OAuth
        /// Validate: Google token hợp lệ
        /// Action: Tự động tạo tài khoản nếu chưa có
        /// Return: AccessToken (JWT) + RefreshToken (HTTP-only cookie)
        /// </summary>
        [HttpPost("google")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            var loginResponse = await _authService.GoogleLoginAsync(request, Response);
            var message = _messageService.GetMessage(ErrorCodes.SuccessLogin);

            return Ok(ApiResponse<LoginResponse>.Success(
                ErrorCodes.SuccessLogin,
                message,
                loginResponse));
        }

        /// <summary>
        /// [PUBLIC] POST /api/auth/refresh
        /// Refresh access token using refresh token from cookie
        /// Validate: RefreshToken phải hợp lệ và chưa expire
        /// Return: AccessToken mới + RefreshToken mới
        /// </summary>
        [HttpPost("refresh")]
        public async Task<ActionResult<ApiResponse<LoginResponse>>> Refresh()
        {
            string refreshToken = GetRefreshTokenFromCookie();
            var refreshResponse = await _authService.RefreshTokenAsync(refreshToken, Response);
            var message = _messageService.GetMessage(ErrorCodes.SuccessRefreshToken);

            return Ok(ApiResponse<LoginResponse>.Success(
                ErrorCodes.SuccessRefreshToken,
                message,
                refreshResponse));
        }

        /// <summary>
        /// [PUBLIC] POST /api/auth/logout
        /// Đăng xuất và xóa refresh token
        /// Action: Xóa refresh token khỏi database và clear cookie
        /// </summary>
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            string? refreshToken = Request.Cookies["refreshToken"];

            if (!string.IsNullOrEmpty(refreshToken))
            {
                await _authService.LogoutAsync(refreshToken, Response);
            }

            var message = _messageService.GetMessage(ErrorCodes.SuccessLogout);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessLogout,
                message,
                null));
        }

        /// <summary>
        /// [PUBLIC] GET /api/auth/verify-email?token={token}
        /// Xác thực email sau khi register
        /// Validate: Token phải hợp lệ và chưa expire (30 phút)
        /// Action: Set email verified status = true
        /// </summary>
        [HttpGet("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new AppException(
                    ErrorCodes.ValidationRequiredField,
                    StatusCodes.Status400BadRequest);
            }

            await _authService.VerifyEmailLinkAsync(token);
            var message = _messageService.GetMessage(ErrorCodes.SuccessVerifyEmail);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessVerifyEmail,
                message,
                null));
        }

        /// <summary>
        /// [PUBLIC] POST /api/auth/resend-email-verify
        /// Gửi lại email xác thực
        /// Validate:
        /// - Email tồn tại
        /// - Email chưa được xác thực
        /// Action: Gửi email mới với token mới (expire 30 phút)
        /// </summary>
        [HttpPost("resend-email-verify")]
        public async Task<IActionResult> ResendEmailVerify([FromBody] ResendVerifyEmailRequest request)
        {
            await _authService.ResendVerifyEmailAsync(request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessResendEmailVerify);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessResendEmailVerify,
                message,
                null));
        }

        /// <summary>
        /// [PUBLIC] POST /api/auth/forgot
        /// Gửi link reset password qua email
        /// Validate: Email tồn tại
        /// Action: Gửi email với reset token (expire 15 phút, lưu trong Redis)
        /// </summary>
        [HttpPost("forgot")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            await _authService.SendResetPasswordLinkAsync(request.Email);
            var message = _messageService.GetMessage(ErrorCodes.SuccessSendForgotLink);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessSendForgotLink,
                message,
                null));
        }

        /// <summary>
        /// [PUBLIC] GET /api/auth/verify-reset-token?token={token}
        /// Kiểm tra reset password token có hợp lệ không
        /// Validate: Token tồn tại trong Redis và chưa expire
        /// Use case: Frontend validate token trước khi show reset password form
        /// </summary>
        [HttpGet("verify-reset-token")]
        public async Task<IActionResult> VerifyResetToken([FromQuery] string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new AppException(
                    ErrorCodes.ValidationRequiredField,
                    StatusCodes.Status400BadRequest);
            }

            var isValid = await _authService.VerifyResetTokenAsync(token);

            if (!isValid)
            {
                throw new AppException(
                    ErrorCodes.ValidationInvalidToken,
                    StatusCodes.Status400BadRequest);
            }

            var message = _messageService.GetMessage(ErrorCodes.SuccessVerifyEmail);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessVerifyEmail,
                message,
                null));
        }

        /// <summary>
        /// [PUBLIC] POST /api/auth/reset-password
        /// Reset password với token từ email
        /// Validate:
        /// - Token hợp lệ và chưa expire
        /// - New password đủ mạnh
        /// Action: Update password và xóa token khỏi Redis
        /// </summary>
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            await _authService.ResetPasswordAsync(request.Token, request.NewPassword);
            var message = _messageService.GetMessage(ErrorCodes.SuccessResetPassword);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessResetPassword,
                message,
                null));
        }
    }
}
