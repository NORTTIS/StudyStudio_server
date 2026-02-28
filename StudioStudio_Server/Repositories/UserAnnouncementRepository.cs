using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    public class UserAnnouncementRepository : IUserAnnouccementRepository
    {
        private readonly StudioDbContext _context;
        public UserAnnouncementRepository(StudioDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(UserAnnouncement userAnnouncement)
        {
            _context.UserAnnouncements.Add(userAnnouncement);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid userAnnouncementId)
        {
            var annouce = await _context.UserAnnouncements.FirstOrDefaultAsync(a => a.UserAnnouncementId == userAnnouncementId);

            if (annouce == null) return;

            annouce.IsDelete = true;
            var now = DateTime.UtcNow;
            annouce.UpdatedAt = now;
            await _context.SaveChangesAsync();
        }

        public async Task<List<UserAnnouncement>> GetByUserIdAsync(Guid userId)
        {
            return await _context.UserAnnouncements
                .Where(ua => ua.MetionedId == userId && !ua.IsDelete)
                .OrderByDescending(ua => ua.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<UserAnnouncement?> GetByIdAsync(Guid userAnnouncementId)
        {
            return await _context.UserAnnouncements
                .FirstOrDefaultAsync(ua => ua.UserAnnouncementId == userAnnouncementId && !ua.IsDelete);
        }

        public async Task UpdateAsync(UserAnnouncement userAnnouncement)
        {
            userAnnouncement.UpdatedAt = DateTime.UtcNow;
            _context.UserAnnouncements.Update(userAnnouncement);
            await _context.SaveChangesAsync();
        }
    }
}
