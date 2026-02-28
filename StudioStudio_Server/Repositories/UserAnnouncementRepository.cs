using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository xử lý các thao tác CRUD với UserAnnouncement entity
    /// </summary>
    public class UserAnnouncementRepository : IUserAnnouccementRepository
    {
        private readonly StudioDbContext _context;

        public UserAnnouncementRepository(StudioDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Thêm mới một UserAnnouncement vào database
        /// </summary>
        public async Task AddAsync(UserAnnouncement userAnnouncement)
        {
            _context.UserAnnouncements.Add(userAnnouncement);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Soft delete UserAnnouncement (IsDelete = true)
        /// </summary>
        public async Task DeleteAsync(Guid userAnnouncementId)
        {
            var userAnnouncement = await _context.UserAnnouncements
                .FirstOrDefaultAsync(a => a.UserAnnouncementId == userAnnouncementId);

            if (userAnnouncement == null)
            {
                return;
            }

            userAnnouncement.IsDelete = true;
            userAnnouncement.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Lấy danh sách UserAnnouncements của user theo userId
        /// Điều kiện: MetionedId = userId AND IsDelete = false
        /// Sắp xếp: CreatedAt DESC (mới nhất trước)
        /// </summary>
        public async Task<List<UserAnnouncement>> GetByUserIdAsync(Guid userId)
        {
            return await _context.UserAnnouncements
                .Where(ua => ua.MetionedId == userId && !ua.IsDelete)
                .OrderByDescending(ua => ua.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Lấy UserAnnouncement theo ID
        /// Điều kiện: UserAnnouncementId = id AND IsDelete = false
        /// </summary>
        public async Task<UserAnnouncement?> GetByIdAsync(Guid userAnnouncementId)
        {
            return await _context.UserAnnouncements
                .FirstOrDefaultAsync(ua => 
                    ua.UserAnnouncementId == userAnnouncementId && 
                    !ua.IsDelete);
        }

        /// <summary>
        /// Cập nhật UserAnnouncement (set UpdatedAt = UtcNow)
        /// </summary>
        public async Task UpdateAsync(UserAnnouncement userAnnouncement)
        {
            userAnnouncement.UpdatedAt = DateTime.UtcNow;
            _context.UserAnnouncements.Update(userAnnouncement);
            await _context.SaveChangesAsync();
        }
    }
}
