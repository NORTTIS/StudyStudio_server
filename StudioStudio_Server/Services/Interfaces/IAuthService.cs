using Microsoft.AspNetCore.Identity.Data;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IAuthService
    {
        Task RegisterAsync(RegisterRequests registerRequest);
        Task<LoginResponse> LoginAsync(LoginRequests loginRequest, HttpResponse response);
        Task<LoginResponse> RefreshTokenAsync(string refreshToken, HttpResponse response);
        Task LogoutAsync(string refreshToken, HttpResponse response);
        Task<LoginResponse> GoogleLoginAsync(GoogleLoginRequest request, HttpResponse response);
        Task VerifyEmailLinkAsync(string token);
        Task SendResetPasswordLinkAsync(string email);
        Task ResetPasswordAsync(string token, string newPassword);
        Task<bool> VerifyResetTokenAsync(string token);

        Task ResendVerifyEmailAsync(ResendVerifyEmailRequest resendVerifyEmailRequest);
    }
}
