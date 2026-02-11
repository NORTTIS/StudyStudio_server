using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IPasswordResetCacheService
    {
        Task StoreResetTokenAsync(string email, string token, Guid userId, TimeSpan expiry);
        Task<PasswordResetDataRedis?> GetResetDataByTokenAsync(string token);
        Task InvalidateResetTokenAsync(string email);
    }
}
