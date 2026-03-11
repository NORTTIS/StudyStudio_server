using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository handling operations with RefreshToken entity
    /// </summary>
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly StudioDbContext _context;

        public RefreshTokenRepository(StudioDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get valid refresh token
        /// Condition: Token = {token} AND IsRevoked = false AND ExpiresAt > UtcNow
        /// Include: User
        /// </summary>
        public async Task<RefreshToken?> GetValidAsync(string token)
        {
            return await _context.RefreshToken
                .Include(x => x.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Token == token &&
                    !x.IsRevoked &&
                    x.ExpiresAt > DateTime.UtcNow);
        }

        /// <summary>
        /// Add new refresh token to database
        /// </summary>
        public async Task AddAsync(RefreshToken token)
        {
            _context.RefreshToken.Add(token);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Revoke refresh token (set IsRevoked = true)
        /// Use case: Logout, token rotation
        /// Uses ExecuteUpdateAsync for better concurrency handling
        /// </summary>
        public async Task RevokeAsync(RefreshToken token)
        {
            await _context.RefreshToken
                .Where(t => t.Id == token.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.IsRevoked, true));
        }

        /// <summary>
        /// Revoke all active refresh tokens for a specific user
        /// Use case: Password reset, account security
        /// Uses ExecuteUpdateAsync for better concurrency handling
        /// </summary>
        public async Task<int> RevokeAllUserTokensAsync(Guid userId)
        {
            var revokedCount = await _context.RefreshToken
                .Where(t => t.UserId == userId && !t.IsRevoked)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.IsRevoked, true));

            return revokedCount;
        }

        /// <summary>
        /// Delete all expired or revoked refresh tokens for a specific user
        /// Removes: IsRevoked = true OR ExpiresAt < UtcNow
        /// Uses ExecuteDeleteAsync for better concurrency handling
        /// </summary>
        public async Task<int> CleanupUserTokensAsync(Guid userId)
        {
            var now = DateTime.UtcNow;
            
            var deletedCount = await _context.RefreshToken
                .Where(t => t.UserId == userId && (t.IsRevoked || t.ExpiresAt < now))
                .ExecuteDeleteAsync();

            return deletedCount;
        }

        /// <summary>
        /// Delete all expired or revoked refresh tokens globally
        /// Removes: IsRevoked = true OR ExpiresAt < UtcNow
        /// Uses ExecuteDeleteAsync for better concurrency handling
        /// </summary>
        public async Task<int> CleanupExpiredTokensAsync()
        {
            var now = DateTime.UtcNow;
            
            var deletedCount = await _context.RefreshToken
                .Where(t => t.IsRevoked || t.ExpiresAt < now)
                .ExecuteDeleteAsync();

            return deletedCount;
        }
    }
}
