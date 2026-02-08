using Microsoft.AspNetCore.Identity.Data;
using StudioStudio_Server.Models.DTOs;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IAuthService
    {
        Task RegisterAsync(Models.DTOs.RegisterRequest registerRequest);
        Task<string> LoginAsync(Models.DTOs.LoginRequest loginRequest, HttpResponse response);
        Task<string> RefreshTokenAsync(string refreshToken, HttpResponse response);
        Task LogoutAsync(string refreshToken);
        Task<string> GoogleLoginAsync(GoogleLoginRequest request, HttpResponse response);
    }
}
