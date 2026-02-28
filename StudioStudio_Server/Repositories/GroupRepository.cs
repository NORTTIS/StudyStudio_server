using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository xử lý các thao tác CRUD với Group entity
    /// </summary>
    public class GroupRepository : IGroupRepository
    {
        private readonly StudioDbContext _db;

        public GroupRepository(StudioDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Lấy danh sách groups mà user tham gia
        /// Điều kiện: Participants contains {userId} AND IsActive = true
        /// Include: Participants
        /// Sắp xếp: CreatedAt DESC
        /// </summary>
        public async Task<List<Group>> GetUserGroupsAsync(Guid userId)
        {
            return await _db.Groups
                .Where(g => g.Participants.Any(p => p.UserId == userId) && g.IsActive)
                .Include(g => g.Participants)
                .OrderByDescending(g => g.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Lấy group theo ID
        /// Điều kiện: GroupId = {groupId} AND IsActive = true
        /// Include: Participants
        /// </summary>
        public async Task<Group?> GetByIdAsync(Guid groupId)
        {
            return await _db.Groups
                .Include(g => g.Participants)
                .FirstOrDefaultAsync(g => g.GroupId == groupId && g.IsActive);
        }

        /// <summary>
        /// Lấy group với chi tiết (read-only)
        /// Điều kiện: GroupId = {groupId} AND IsActive = true
        /// Include: Participants
        /// Use case: View group details (không update)
        /// </summary>
        public async Task<Group?> GetGroupWithDetailsAsync(Guid groupId)
        {
            return await _db.Groups
                .Include(g => g.Participants)
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.GroupId == groupId && g.IsActive);
        }

        /// <summary>
        /// Lấy danh sách groups trong studio
        /// Điều kiện: StudioId = {studioId} AND IsActive = true
        /// Sắp xếp: GroupName DESC, CreatedAt DESC
        /// </summary>
        public async Task<List<Group>> GetStudioGroupsAsync(Guid studioId)
        {
            return await _db.Groups
                .Where(g => g.StudioId == studioId && g.IsActive)
                .OrderByDescending(g => g.GroupName)
                .ThenByDescending(g => g.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Đếm số groups do user tạo (Owner)
        /// Điều kiện: Participants contains {userId với Role = Owner} AND IsActive = true
        /// Use case: Check giới hạn số groups có thể tạo
        /// </summary>
        public async Task<int> CountGroupsCreatedByUserAsync(Guid userId)
        {
            return await _db.Groups
                .Where(g => g.Participants.Any(p => p.UserId == userId && p.Role == GroupRole.Owner) && g.IsActive)
                .CountAsync();
        }

        /// <summary>
        /// Đếm số groups trong studio
        /// Điều kiện: StudioId = {studioId} AND IsActive = true
        /// </summary>
        public async Task<int> GetGroupCountByStudioIdAsync(Guid studioId)
        {
            return await _db.Groups
                .Where(g => g.StudioId == studioId && g.IsActive)
                .CountAsync();
        }

        /// <summary>
        /// Kiểm tra group name có tồn tại trong studio không
        /// Điều kiện: StudioId = {studioId} AND GroupName = {groupName} AND IsActive = true
        /// Use case: Validate khi tạo group mới
        /// </summary>
        public async Task<bool> GroupNameExistsInStudioAsync(Guid? studioId, string groupName)
        {
            return await _db.Groups
                .AnyAsync(g => g.StudioId == studioId &&
                              g.GroupName == groupName &&
                              g.IsActive);
        }

        /// <summary>
        /// Kiểm tra group name có tồn tại trong studio (exclude group đang update)
        /// Điều kiện: StudioId = {studioId} AND GroupName = {groupName} AND GroupId != {excludeGroupId} AND IsActive = true
        /// Use case: Validate khi update group name
        /// </summary>
        public async Task<bool> GroupNameExistsInStudioExcludingGroupAsync(
            Guid? studioId,
            string groupName,
            Guid excludeGroupId)
        {
            return await _db.Groups
                .AnyAsync(g => g.StudioId == studioId &&
                              g.GroupName == groupName &&
                              g.GroupId != excludeGroupId &&
                              g.IsActive);
        }

        /// <summary>
        /// Kiểm tra user có phải Owner của group không
        /// Điều kiện: GroupId = {groupId} AND IsActive = true AND Participants contains {userId với Role = Owner}
        /// </summary>
        public async Task<bool> IsUserGroupOwnerAsync(Guid groupId, Guid userId)
        {
            return await _db.Groups
                .Where(g => g.GroupId == groupId && g.IsActive)
                .AnyAsync(g => g.Participants.Any(p => p.UserId == userId && p.Role == GroupRole.Owner));
        }

        /// <summary>
        /// Thêm group mới vào database
        /// </summary>
        public async Task AddAsync(Group group)
        {
            _db.Groups.Add(group);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Update group information
        /// Auto-set: UpdatedAt = UtcNow
        /// </summary>
        public async Task UpdateAsync(Group group)
        {
            group.UpdatedAt = DateTime.UtcNow;
            _db.Groups.Update(group);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Soft delete group
        /// Set IsActive = false, UpdatedAt = UtcNow
        /// Note: Participants, tasks, messages vẫn giữ nguyên trong DB
        /// </summary>
        public async Task DeleteAsync(Group group)
        {
            group.IsActive = false;
            group.UpdatedAt = DateTime.UtcNow;
            _db.Groups.Update(group);
            await _db.SaveChangesAsync();
        }
    }
}
