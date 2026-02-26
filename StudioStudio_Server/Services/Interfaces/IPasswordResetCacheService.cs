using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IPasswordResetCacheService
    {
        Task<PasswordResetDataRedis?> GetResetDataByTokenAsync(string token);
        Task<bool> CanSendResetEmailAsync(string email);
        Task StoreResetTokenAsync(string email, string token, Guid userId, TimeSpan expiry);
        Task InvalidateResetTokenAsync(string email);
        Task IncrementSendCountAsync(string email);
    }
}
