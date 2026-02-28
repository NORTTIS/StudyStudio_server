using Microsoft.EntityFrameworkCore;
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

        public async Task<Announcement?> GetByIdAsync(Guid announcementId)
        {
            return await _context.Announcements
                .FirstOrDefaultAsync(a => a.AnnouncementId == announcementId);
        }

        public async Task<List<Announcement>> GetAllActiveAsync()
        {
            return await _context.Announcements
                .Where(a => a.IsActive)
                .OrderByDescending(a => a.PublishedAt ?? a.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Announcement>> GetAllAsync()
        {
            return await _context.Announcements
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task UpdateAsync(Announcement announcement)
        {
            _context.Announcements.Update(announcement);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Announcement announcement)
        {
            _context.Announcements.Remove(announcement);
            await _context.SaveChangesAsync();
        }
    }
}
