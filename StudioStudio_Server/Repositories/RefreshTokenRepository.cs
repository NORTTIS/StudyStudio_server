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
        /// </summary>
        public async Task RevokeAsync(RefreshToken token)
        {
            token.IsRevoked = true;
            await _context.SaveChangesAsync();
        }
    }
}
