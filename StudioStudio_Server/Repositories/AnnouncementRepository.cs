using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository handling CRUD operations with Announcement entity
    /// </summary>
    public class AnnouncementRepository : IAnnouncementRepository
    {
        private readonly StudioDbContext _context;

        public AnnouncementRepository(StudioDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Add new announcement to database
        /// </summary>
        public async Task AddAsync(Announcement announcement)
        {
            _context.Announcements.Add(announcement);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Get announcement by ID (no conditions)
        /// </summary>
        public async Task<Announcement?> GetByIdAsync(Guid announcementId)
        {
            return await _context.Announcements
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AnnouncementId == announcementId);
        }

        /// <summary>
        /// Get all active announcements (IsActive = true)
        /// Order by: PublishedAt DESC (priority), then CreatedAt DESC
        /// </summary>
        public async Task<List<Announcement>> GetAllActiveAsync()
        {
            return await _context.Announcements
                .Where(a => a.IsActive)
                .OrderByDescending(a => a.PublishedAt ?? a.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Get all announcements (including inactive)
        /// Order by: CreatedAt DESC (newest first)
        /// </summary>
        public async Task<List<Announcement>> GetAllAsync()
        {
            return await _context.Announcements
                .OrderByDescending(a => a.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Get announcements by list of IDs (bulk load to prevent N+1 queries)
        /// Returns only announcements that exist in the database
        /// </summary>
        public async Task<List<Announcement>> GetByIdsAsync(List<Guid> announcementIds)
        {
            if (announcementIds == null || !announcementIds.Any())
            {
                return new List<Announcement>();
            }

            return await _context.Announcements
                .Where(a => announcementIds.Contains(a.AnnouncementId))
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Update announcement information
        /// </summary>
        public async Task UpdateAsync(Announcement announcement)
        {
            _context.Announcements.Update(announcement);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Delete announcement from database (hard delete)
        /// </summary>
        public async Task DeleteAsync(Announcement announcement)
        {
            _context.Announcements.Remove(announcement);
            await _context.SaveChangesAsync();
        }
    }
}
