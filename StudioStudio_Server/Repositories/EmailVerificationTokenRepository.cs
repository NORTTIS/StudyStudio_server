using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    public class EmailVerificationTokenRepository : IEmailVerificationTokenRepository
    {
        private readonly StudioDbContext _context;
        public EmailVerificationTokenRepository(StudioDbContext context)
        {
            _context = context;
        }
        public async Task AddAsync(EmailVerificationToken token)
        {
            _context.EmailVerificationTokens.Add(token);
            await _context.SaveChangesAsync();
        }

        public async Task<EmailVerificationToken?> GetValidAsync(string token)
        {
            return await _context.EmailVerificationTokens
                .Include(x => x.User)
                .FirstOrDefaultAsync(x =>
                    x.Token == token && !x.IsUsed &&
                    x.ExpiresAt > DateTime.UtcNow);
        }

        public async Task MaskAsUsed(EmailVerificationToken token)
        {
            token.IsUsed = true;
            _context.EmailVerificationTokens.Update(token);
            await _context.SaveChangesAsync();
        }

    }
}
