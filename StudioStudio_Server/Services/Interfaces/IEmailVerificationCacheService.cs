using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IEmailVerificationCacheService
    {
        Task<EmailVerificationDataRedis?> GetVerificationDataByTokenAsync(string token);
        Task<bool> CanSendVerificationEmailAsync(string email);
        Task StoreVerificationTokenAsync(string email, string token, Guid userId, TimeSpan expiry);
        Task InvalidateVerificationTokenAsync(string email);
        Task IncrementSendCountAsync(string email);
    }
}
