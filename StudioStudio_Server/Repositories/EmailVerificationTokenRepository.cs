using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository handling operations with EmailVerificationToken entity
    /// Manages email verification tokens for user registration and email changes
    /// </summary>
    public class EmailVerificationTokenRepository : IEmailVerificationTokenRepository
    {
        private readonly StudioDbContext _context;
        
        public EmailVerificationTokenRepository(StudioDbContext context)
        {
            _context = context;
        }
        
        /// <summary>
        /// Add new email verification token to database
        /// </summary>
        public async Task AddAsync(EmailVerificationToken token)
        {
            _context.EmailVerificationTokens.Add(token);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Get valid email verification token
        /// Condition: Token = {token} AND IsUsed = false AND ExpiresAt > UtcNow AND User.Status != Deleted
        /// Include: User info
        /// Use case: Verify email during registration or email change
        /// </summary>
        public async Task<EmailVerificationToken?> GetValidAsync(string token)
        {
            return await _context.EmailVerificationTokens
                .Include(x => x.User)
                .FirstOrDefaultAsync(x =>
                    x.Token == token && !x.IsUsed &&
                    x.ExpiresAt > DateTime.UtcNow && x.User.Status != UserStatus.Deleted);
        }

        /// <summary>
        /// Mark token as used after successful verification
        /// Set IsUsed = true
        /// </summary>
        public async Task MaskAsUsed(EmailVerificationToken token)
        {
            token.IsUsed = true;
            _context.EmailVerificationTokens.Update(token);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Invalidate all unused tokens for a user
        /// Set IsUsed = true for all unused tokens of the user
        /// Use case: When user requests new verification token or changes email
        /// </summary>
        public async Task InvalidateTokensAsync(Guid userId)
        {
            var tokens = await _context.EmailVerificationTokens
                .Where(x => x.UserId == userId && !x.IsUsed)
                .ToListAsync();

            foreach (var token in tokens)
            {
                token.IsUsed = true;
            }

            await _context.SaveChangesAsync();
        }
    }
}
