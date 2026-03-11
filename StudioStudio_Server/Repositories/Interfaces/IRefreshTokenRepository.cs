using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IRefreshTokenRepository
    {
        Task<RefreshToken?> GetValidAsync(string token);
        Task AddAsync(RefreshToken token);
        Task RevokeAsync(RefreshToken token);
        
        /// <summary>
        /// Revoke all active refresh tokens for a specific user
        /// Returns: Number of tokens revoked
        /// </summary>
        Task<int> RevokeAllUserTokensAsync(Guid userId);
        
        /// <summary>
        /// Delete all expired or revoked refresh tokens for a specific user
        /// Returns: Number of tokens deleted
        /// </summary>
        Task<int> CleanupUserTokensAsync(Guid userId);
        
        /// <summary>
        /// Delete all expired or revoked refresh tokens globally (for maintenance)
        /// Returns: Number of tokens deleted
        /// </summary>
        Task<int> CleanupExpiredTokensAsync();
    }
}
