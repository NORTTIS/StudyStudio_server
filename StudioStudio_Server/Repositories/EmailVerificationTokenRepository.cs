using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
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

        public async Task<EmailVerificationToken?> GetValidAsync(string token, EmailTokenType type)
        {
            return await _context.EmailVerificationTokens
                .Include(x => x.User)
                .FirstOrDefaultAsync(x =>
                    x.Token == token &&
                    x.Type == type &&
                    !x.IsUsed &&
                    x.ExpiresAt > DateTime.UtcNow);
        }

        public async Task MarkAsUsedAsync(EmailVerificationToken token)
        {
            token.IsUsed = true;
            _context.Update(token);
            await _context.SaveChangesAsync();
        }

        public async Task InvalidateAllAsync(Guid userId, EmailTokenType type)
        {
            var tokens = await _context.EmailVerificationTokens
                .Where(x =>
                    x.UserId == userId &&
                    x.Type == type &&
                    !x.IsUsed
                )
                .ToListAsync();

            if (!tokens.Any())
                return;

            foreach (var token in tokens)
            {
                token.IsUsed = true;
            }

            await _context.SaveChangesAsync();
        }


        public async Task<EmailVerificationToken?> GetLatestAsync(Guid userId, EmailTokenType type)
        {
            return await _context.EmailVerificationTokens
                .Where(x =>
                    x.UserId == userId &&
                    x.Type == type &&
                    !x.IsUsed
                )
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();
        }
    }
}
