using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Service interface for Authentication and Authorization
    /// </summary>
    public interface IAuthService
    {
        Task RegisterAsync(RegisterRequests registerRequest);
        Task<LoginResponse> LoginAsync(LoginRequests loginRequest, HttpResponse response);
        Task<LoginResponse> GoogleLoginAsync(GoogleLoginRequest request, HttpResponse response);
        Task<LoginResponse> RefreshTokenAsync(string refreshToken, HttpResponse response);
        Task LogoutAsync(string refreshToken, HttpResponse response);
        Task VerifyEmailLinkAsync(string token);
        Task ResendVerifyEmailAsync(ResendVerifyEmailRequest resendVerifyEmailRequest);
        Task SendResetPasswordLinkAsync(string email);
        Task<bool> VerifyResetTokenAsync(string token);
        Task ResetPasswordAsync(string token, string newPassword);
    }
}
