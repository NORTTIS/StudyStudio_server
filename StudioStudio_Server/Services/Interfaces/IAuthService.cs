using Microsoft.AspNetCore.Identity.Data;
using StudioStudio_Server.Models.DTOs.Request;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IAuthService
    {
        Task RegisterAsync(RegisterRequests registerRequest);
        Task<string> LoginAsync(LoginRequests loginRequest, HttpResponse response);
        Task<string> RefreshTokenAsync(string refreshToken, HttpResponse response);
        Task LogoutAsync(string refreshToken, HttpResponse response);
        Task<string> GoogleLoginAsync(GoogleLoginRequest request, HttpResponse response);
        Task VerifyEmailAsync(string token);
        Task SendResetPasswordLinkAsync(string email);
        Task ResendEmailAsync(ResendEmailRequest request);
    }
}
