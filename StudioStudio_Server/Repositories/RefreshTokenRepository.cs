using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly StudioDbContext _context;
        public RefreshTokenRepository(StudioDbContext context)
        {
            _context = context;
        }
        public async Task AddAsync(RefreshToken token)
        {
            _context.RefreshToken.Add(token);
            await _context.SaveChangesAsync();
        }
        public async Task<RefreshToken?> GetValidAsync(string token)
        {
            return await _context.RefreshToken
                .Include(x => x.User)
                .FirstOrDefaultAsync(x =>
                    x.Token == token &&
                    !x.IsRevoked &&
                    x.ExpiresAt > DateTime.UtcNow);
        }

        public async Task RevokeAsync(RefreshToken token)
        {
            token.IsRevoked = true;
            await _context.SaveChangesAsync();
        }
    }
}
