using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository xử lý các thao tác CRUD với UserAnnouncement entity
    /// </summary>
    public class UserAnnouncementRepository(StudioDbContext context) : IUserAnnouccementRepository
    {
        /// <summary>
        /// Thêm mới một UserAnnouncement vào database
        /// </summary>
        public async Task AddAsync(UserAnnouncement userAnnouncement)
        {
            context.UserAnnouncements.Add(userAnnouncement);
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Soft delete UserAnnouncement (IsDelete = true)
        /// </summary>
        public async Task DeleteAsync(Guid userAnnouncementId)
        {
            var userAnnouncement = await context.UserAnnouncements
                .FirstOrDefaultAsync(a => a.UserAnnouncementId == userAnnouncementId);

            if (userAnnouncement == null)
            {
                return;
            }

            userAnnouncement.IsDelete = true;
            userAnnouncement.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }


        /// <summary>
        /// Lấy UserAnnouncement theo ID
        /// Điều kiện: UserAnnouncementId = id AND IsDelete = false
        /// </summary>
        public async Task<UserAnnouncement?> GetByIdAsync(Guid userAnnouncementId)
        {
            return await context.UserAnnouncements
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
            context.UserAnnouncements.Update(userAnnouncement);
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Lấy UserAnnouncement theo AnnouncementId và MentionedId
        /// </summary>
        public async Task<UserAnnouncement?> GetByAnnouncementAndUserAsync(Guid announcementId, Guid userId)
        {
            return await context.UserAnnouncements
                .FirstOrDefaultAsync(ua =>
                    ua.AnnouncementId == announcementId &&
                    ua.MentionedId == userId &&
                    !ua.IsDelete);
        }
    }
}
