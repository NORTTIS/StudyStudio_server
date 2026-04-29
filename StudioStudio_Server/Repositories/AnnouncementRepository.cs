using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository handling CRUD operations with Announcement entity
    /// </summary>
    public class AnnouncementRepository(StudioDbContext context) : IAnnouncementRepository
    {
        /// <summary>
        /// Add new announcement to database
        /// </summary>
        public async Task AddAsync(Announcement announcement)
        {
            context.Announcements.Add(announcement);
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Get announcement by ID (no conditions)
        /// </summary>
        public async Task<Announcement?> GetByIdAsync(Guid announcementId)
        {
            return await context.Announcements
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AnnouncementId == announcementId);
        }


        /// <summary>
        /// Get system-wide announcements (not @mentions)
        /// Admin-facing query intentionally returns all system announcement types,
        /// including inactive or unpublished records, so the dashboard can manage drafts.
        /// Order by: CreatedAt DESC (newest first)
        /// </summary>
        public async Task<List<Announcement>> GetSystemAnnouncementsAsync()
        {
            return await context.Announcements
                .Where(a => new[]
                {
                    AnnouncementType.Info,
                    AnnouncementType.Warning,
                    AnnouncementType.Maintenance,
                    AnnouncementType.Promotion
                }.Contains(a.Type))
                .OrderByDescending(a => a.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Lấy tất cả announcements cho user trong 1 query duy nhất.
        /// - Type 0-3: tất cả, kèm IsRead (từ UserAnnouncement)
        /// - Type 4-17: tất cả được mention, kèm IsRead
        /// Filter: PublishedAt must not be in the future
        /// </summary>
        public async Task<(List<Announcement> Announcements, int TotalCount)> GetAllForUserAsync(Guid userId, int page, int pageSize)
        {
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 10 : pageSize;

            var now = DateTime.UtcNow;

            var query = context.Announcements
                .Include(a => a.UserAnnouncements.Where(ua => ua.MentionedId == userId))
                .Where(a => a.IsActive &&
                       (a.PublishedAt == null || a.PublishedAt <= now) &&
                       (
                    // Loại 1: Type 0-3
                    (a.Type >= AnnouncementType.Info && a.Type <= AnnouncementType.Promotion)
                    // Loại 2: Type 4-17 mà user được mention
                    || a.UserAnnouncements.Any(ua => ua.MentionedId == userId)))
                .AsNoTracking();

            var totalCount = await query.CountAsync();

            var announcements = await query
                .OrderByDescending(a => a.PublishedAt ?? a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (announcements, totalCount);
        }


        /// <summary>
        /// Update announcement information
        /// </summary>
        public async Task UpdateAsync(Announcement announcement)
        {
            context.Announcements.Update(announcement);
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Delete announcement from database (hard delete)
        /// </summary>
        public async Task DeleteAsync(Announcement announcement)
        {
            context.Announcements.Remove(announcement);
            await context.SaveChangesAsync();
        }

    }
}
