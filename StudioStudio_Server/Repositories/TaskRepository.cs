using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository x? l? các thao tác CRUD v?i TaskItem entity
    /// </summary>
    public class TaskRepository : ITaskRepository
    {
        private readonly StudioDbContext _context;

        public TaskRepository(StudioDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// L?y task theo ID
        /// Ði?u ki?n: TaskId = {taskId} AND IsPendingDeleted = false
        /// </summary>
        public async Task<TaskItem?> GetByIdAsync(Guid taskId)
        {
            return await _context.Tasks
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TaskId == taskId && !t.IsPendingDeleted);
        }

        /// <summary>
        /// Ð?m s? tasks trong group
        /// Ði?u ki?n: GroupId = {groupId} AND IsPendingDeleted = false
        /// </summary>
        public async Task<int> GetTaskCountByGroupIdAsync(Guid groupId)
        {
            return await _context.Tasks
                .Where(t => t.GroupId == groupId && !t.IsPendingDeleted)
                .CountAsync();
        }

        /// <summary>
        /// Ð?m s? tasks cho nhi?u groups (batch query)
        /// Ði?u ki?n: GroupId IN {groupIds}
        /// Return: Dictionary [GroupId ? TaskCount]
        /// Use case: Hi?n th? task count cho danh sách groups
        /// </summary>
        public async Task<Dictionary<Guid, int>> GetTaskCountByGroupIdsAsync(List<Guid> groupIds)
        {
            var taskCounts = await _context.Tasks
                .Where(t => t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value))
                .GroupBy(t => t.GroupId.Value)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .AsNoTracking()
                .ToListAsync();

            return taskCounts.ToDictionary(tc => tc.GroupId, tc => tc.Count);
        }
    }
}
