using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository handling operations with GroupTaskStatus entity
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
        /// Get list of task statuses for group
        /// Condition: GroupId = {groupId}
        /// Order by: Position ASC (according to Kanban columns order)
        /// </summary>
        public async Task<List<GroupTaskStatus>> GetByGroupIdAsync(Guid groupId)
        {
            return await _context.GroupTaskStatuses
                .Where(s => s.GroupId == groupId && !s.IsDeleted)
                .OrderBy(s => s.Position)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Check if status exists
        /// Condition: StatusId = {statusId}
        /// </summary>
        public async Task<bool> ExistsAsync(Guid statusId)
        {
            return await _context.GroupTaskStatuses
                .AnyAsync(s => s.StatusId == statusId);
        }

        /// <summary>
        /// Add new task status to group
        /// </summary>
        public async Task AddAsync(GroupTaskStatus status)
        {
            _context.GroupTaskStatuses.Add(status);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Add multiple task statuses (batch insert)
        /// Use case: Initialize default statuses when creating group from template
        /// </summary>
        public async Task AddRangeAsync(List<GroupTaskStatus> statuses)
        {
            _context.GroupTaskStatuses.AddRange(statuses);
            await _context.SaveChangesAsync();
        }

        public async Task SoftDeleteAsync(GroupTaskStatus status)
        {
            status.IsDeleted = true;
            _context.GroupTaskStatuses.Update(status);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(GroupTaskStatus status)
        {
            _context.GroupTaskStatuses.Update(status);
            await _context.SaveChangesAsync();
        }

        public async Task<GroupTaskStatus?> GetDetailAsync(Guid statusId)
        {
            return await _context.GroupTaskStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StatusId == statusId && !s.IsDeleted);
        }

        public async Task<List<GroupTaskStatus>> GetByIdsAndGroupIdAsync(List<Guid> statusIds, Guid groupId)
        {
            return await _context.GroupTaskStatuses
                .Where(x => statusIds.Contains(x.StatusId) && x.GroupId == groupId)
                .AsNoTracking()
                .ToListAsync();
        }
        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<bool> NameExistsInGroupAsync(GroupTaskStatus taskStatus)
        {
            return await _context.GroupTaskStatuses.AnyAsync(t =>
                t.StatusName == taskStatus.StatusName &&
                t.GroupId == taskStatus.GroupId &&
                t.StatusId != taskStatus.StatusId
            );
        }
    }
}
