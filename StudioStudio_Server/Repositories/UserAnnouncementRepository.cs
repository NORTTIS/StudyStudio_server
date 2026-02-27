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
            annouce.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
        }
    }
}
