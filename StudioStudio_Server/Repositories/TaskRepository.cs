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

        /// <summary>
        /// Thêm task vào db
        /// </summary>
        public async Task AddAsync(TaskItem task)
        {
            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Restore task
        /// Set IsPendingDeleted = false, UpdatedAt = UtcNow
        /// </summary>
        public async Task RestoreAsync(Guid taskId)
        {
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.TaskId == taskId);
            if (task != null)
            {
                task.IsPendingDeleted = false;
                task.UpdatedAt = DateTime.UtcNow;
                _context.Tasks.Update(task);
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Soft delete task
        /// Set IsPendingDeleted = true, UpdatedAt = UtcNow
        /// </summary>
        public async Task SoftDeleteAsync(Guid taskId)
        {
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.TaskId == taskId);
            if (task != null)
            {
                task.IsPendingDeleted = true;
                task.UpdatedAt = DateTime.UtcNow;
                _context.Tasks.Update(task);
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Update task 
        /// Auto-set: UpdatedAt = UtcNow
        /// </summary>
        public async Task UpdateAsync(TaskItem task)
        {
            task.UpdatedAt = DateTime.UtcNow;
            _context.Tasks.Update(task);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Lấy danh sách soft tasks trong group
        /// Điều kiện: GroupId = {groupId} AND IsPendingDeleted = true
        /// Sắp xếp: UpdatedAt DESC, Title DESC
        /// </summary>
        public async Task<List<TaskItem>> GetSoftDeleteTaskByGroup(Guid groupId)
        {
            return await _context.Tasks
                .Where(t => t.GroupId == groupId && t.IsPendingDeleted)
                .OrderByDescending(t => t.UpdatedAt)
                .ThenByDescending(t => t.Title)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
