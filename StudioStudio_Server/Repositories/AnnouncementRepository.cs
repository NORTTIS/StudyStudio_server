using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    public class AnnouncementRepository : IAnnouncementRepository
    {
        private readonly StudioDbContext _context;
        public AnnouncementRepository(StudioDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Announcement announcement)
        {
            _context.Announcements.Add(announcement);
            await _context.SaveChangesAsync();
        }
    }
}
