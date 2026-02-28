using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository xử lý các thao tác CRUD với Announcement entity
    /// </summary>
    public class AnnouncementRepository : IAnnouncementRepository
    {
        private readonly StudioDbContext _context;

        public AnnouncementRepository(StudioDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Thêm mới một announcement vào database
        /// </summary>
        public async Task AddAsync(Announcement announcement)
        {
            _context.Announcements.Add(announcement);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Lấy announcement theo ID (không có điều kiện)
        /// </summary>
        public async Task<Announcement?> GetByIdAsync(Guid announcementId)
        {
            return await _context.Announcements
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AnnouncementId == announcementId);
        }

        /// <summary>
        /// Lấy tất cả announcements đang active (IsActive = true)
        /// Sắp xếp: PublishedAt DESC (ưu tiên), sau đó CreatedAt DESC
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
        /// Lấy tất cả announcements (bao gồm cả inactive)
        /// Sắp xếp: CreatedAt DESC (mới nhất trước)
        /// </summary>
        public async Task<List<Announcement>> GetAllAsync()
        {
            return await _context.Announcements
                .OrderByDescending(a => a.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Cập nhật thông tin announcement
        /// </summary>
        public async Task UpdateAsync(Announcement announcement)
        {
            _context.Announcements.Update(announcement);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Xóa announcement khỏi database (hard delete)
        /// </summary>
        public async Task DeleteAsync(Announcement announcement)
        {
            _context.Announcements.Remove(announcement);
            await _context.SaveChangesAsync();
        }
    }
}
