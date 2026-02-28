using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository x? l? các thao tác v?i GroupTaskStatus entity
    /// Note: GroupTaskStatus = Kanban columns (To Do, In Progress, Done, etc.)
    /// </summary>
    public class GroupTaskStatusRepository : IGroupTaskStatusRepository
    {
        private readonly StudioDbContext _context;

        public GroupTaskStatusRepository(StudioDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// L?y danh sách task statuses c?a group
        /// Ði?u ki?n: GroupId = {groupId}
        /// S?p x?p: Position ASC (theo th? t? Kanban columns)
        /// </summary>
        public async Task<List<GroupTaskStatus>> GetByGroupIdAsync(Guid groupId)
        {
            return await _context.GroupTaskStatuses
                .Where(s => s.GroupId == groupId)
                .OrderBy(s => s.Position)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Ki?m tra status có t?n t?i không
        /// Ði?u ki?n: StatusId = {statusId}
        /// </summary>
        public async Task<bool> ExistsAsync(Guid statusId)
        {
            return await _context.GroupTaskStatuses
                .AnyAsync(s => s.StatusId == statusId);
        }

        /// <summary>
        /// Thêm task status m?i vào group
        /// </summary>
        public async Task AddAsync(GroupTaskStatus status)
        {
            _context.GroupTaskStatuses.Add(status);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Thêm nhi?u task statuses (batch insert)
        /// Use case: Initialize default statuses khi t?o group t? template
        /// </summary>
        public async Task AddRangeAsync(List<GroupTaskStatus> statuses)
        {
            _context.GroupTaskStatuses.AddRange(statuses);
            await _context.SaveChangesAsync();
        }
    }
}
