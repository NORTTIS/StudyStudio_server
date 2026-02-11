using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IEmailVerificationTokenRepository
    {
        Task AddAsync(EmailVerificationToken token);
        Task<EmailVerificationToken?> GetValidAsync(string token);
        Task MaskAsUsed(EmailVerificationToken token);
        Task InvalidateTokensAsync(Guid userId);
    }
}
