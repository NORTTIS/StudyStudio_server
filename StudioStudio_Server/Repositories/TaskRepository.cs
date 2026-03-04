using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository handling CRUD operations with TaskItem entity
    /// </summary>
    public class TaskRepository : ITaskRepository
    {
        private readonly StudioDbContext _context;

        public TaskRepository(StudioDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get task by ID
        /// Condition: TaskId = {taskId} AND IsPendingDeleted = false
        /// </summary>
        public async Task<TaskItem?> GetByIdAsync(Guid taskId)
        {
            return await _context.Tasks
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TaskId == taskId && !t.IsPendingDeleted);
        }

        /// <summary>
        /// Count tasks in group
        /// Condition: GroupId = {groupId} AND IsPendingDeleted = false
        /// </summary>
        public async Task<int> GetTaskCountByGroupIdAsync(Guid groupId)
        {
            return await _context.Tasks
                .Where(t => t.GroupId == groupId && !t.IsPendingDeleted)
                .CountAsync();
        }

        /// <summary>
        /// Count tasks for multiple groups (batch query)
        /// Condition: GroupId IN {groupIds}
        /// Return: Dictionary [GroupId → TaskCount]
        /// Use case: Display task count for list of groups
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
        /// Get task statistics for group (for AI Q&A)
        /// Include: Total, Completed, Overdue, Nearest deadline
        /// </summary>
        public async Task<TaskSummaryResponse> GetGroupTaskStatisticsAsync(Guid groupId)
        {
            DateTime now = DateTime.UtcNow;

            List<TaskItem> tasks = await _context.Tasks
                .Where(t => t.GroupId == groupId && !t.IsPendingDeleted)
                .AsNoTracking()
                .ToListAsync();

            int totalTasks = tasks.Count;
            int completedTasks = tasks.Count(t => t.GroupStatus != null &&
                                                   t.GroupStatus.StatusName.ToLower().Contains("done"));
            int overdueTasks = tasks.Count(t => t.DueDate.HasValue &&
                                                 t.DueDate.Value < now &&
                                                 (t.GroupStatus == null ||
                                                  !t.GroupStatus.StatusName.ToLower().Contains("done")));

            DateTime? nearestDeadline = tasks
                .Where(t => t.DueDate.HasValue && t.DueDate.Value > now)
                .OrderBy(t => t.DueDate)
                .FirstOrDefault()?.DueDate;

            int completionPercentage = totalTasks > 0
                ? (int)Math.Round((double)completedTasks / totalTasks * 100)
                : 0;

            List<string> riskFlags = new List<string>();
            if (overdueTasks > 0)
            {
                riskFlags.Add($"⚠️ {overdueTasks} overdue task(s)");
            }
            if (nearestDeadline.HasValue && nearestDeadline.Value <= now.AddDays(2))
            {
                riskFlags.Add($"⏰ Nearest deadline: {nearestDeadline:dd/MM/yyyy HH:mm}");
            }

            return new TaskSummaryResponse
            {
                TotalTasks = totalTasks,
                CompletedTasks = completedTasks,
                CompletionPercentage = completionPercentage,
                OverdueTasks = overdueTasks,
                NearestDeadline = nearestDeadline,
                RiskFlags = riskFlags
            };
        }

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

        public async Task<List<TaskItem>> GetAllTasksByStatusId(Guid statusId)
        {
            return await _context.Tasks
                .Where(t => t.GroupStatusId == statusId && !t.IsPendingDeleted)
                .AsNoTracking()
                .OrderBy(t => t.Position)
                .ToListAsync();
        }
    }
}
