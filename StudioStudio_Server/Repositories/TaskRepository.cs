using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using System.Collections.Generic;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository handling CRUD operations with TaskItem entity
    /// </summary>
    public class TaskRepository : ITaskRepository
    {
        private readonly StudioDbContext _context;
        private const int STEP = 1000;
        private const int MAX_TRY = 3;
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

        public async Task<TaskItem?> GetDeletedByIdAsync(Guid taskId)
        {
            return await _context.Tasks
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TaskId == taskId && t.IsPendingDeleted);
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
        public async Task RestoreAsync(TaskItem task)
        {
            task.IsPendingDeleted = false;
            task.UpdatedAt = DateTime.UtcNow;
            _context.Tasks.Update(task);
            await _context.SaveChangesAsync();
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
                .OrderBy(t => t.UpdatedAt)
                .ThenByDescending(t => t.Title)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<TaskItem>> GetAllTasksByStatusIdAsync(Guid statusId)
        {
            return await _context.Tasks
                .Where(t => t.GroupStatusId == statusId && !t.IsPendingDeleted)
                .AsNoTracking()
                .OrderBy(t => t.Position)
                .ToListAsync();
        }
        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<Dictionary<Guid, List<TaskItem>>> GetListTasksByListStatusId(List<Guid> listStatusIds)
        {
            Dictionary<Guid, List<TaskItem>> result = new Dictionary<Guid, List<TaskItem>>();

            foreach (var statusId in listStatusIds)
            {
                var tasks = await _context.Tasks
                    .Where(t => t.GroupStatusId == statusId && !t.IsPendingDeleted)
                    .AsNoTracking()
                    .OrderBy(t => t.Position)
                    .ToListAsync();

                result[statusId] = tasks;
            }

            return result;
        }

        /// <summary>
        /// Reorder (và đổi status nếu cần) cho một task dựa trên vị trí kéo thả.
        /// Hỗ trợ: kéo trong cùng status, kéo sang status khác.
        /// </summary>
        public async Task ReorderTaskAsync(Guid taskId, Guid targetStatusId, Guid? prevTaskId, Guid? nextTaskId)
        {
            if (!prevTaskId.HasValue && !nextTaskId.HasValue)
            {
                throw new InvalidOperationException("Both prevTaskId and nextTaskId cannot be null");
            }

            for (int attemp = 1; attemp <= MAX_TRY; attemp++)
            {
                using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

                try
                {
                    var targetStatus = await _context.GroupTaskStatuses
                        .FirstOrDefaultAsync(s => s.StatusId == targetStatusId && !s.IsDeleted)
                    ?? throw new InvalidOperationException($"Status {targetStatusId} not found");

                    // Load prev/next task (phải thuộc đúng targetStatus)
                    var prev = prevTaskId.HasValue
                        ? await _context.Tasks
                            .FirstOrDefaultAsync(t => t.TaskId == prevTaskId.Value
                                                   && t.GroupStatusId == targetStatusId
                                                   && !t.IsPendingDeleted)
                        : null;

                    var next = nextTaskId.HasValue
                        ? await _context.Tasks
                            .FirstOrDefaultAsync(t => t.TaskId == nextTaskId.Value
                                                   && t.GroupStatusId == targetStatusId
                                                   && !t.IsPendingDeleted)
                        : null;

                    long newPos;

                    if (prev != null && next != null)
                    {
                        long gap = next.Position - prev.Position;

                        if (gap <= 1)
                        {
                            await RebalanceTasksInStatusInternalAsync(targetStatusId);

                            // Reload sau rebalance
                            prev = await _context.Tasks
                                .FirstOrDefaultAsync(t => t.TaskId == prevTaskId!.Value && !t.IsPendingDeleted);
                            next = await _context.Tasks
                                .FirstOrDefaultAsync(t => t.TaskId == nextTaskId!.Value && !t.IsPendingDeleted);
                        }

                        newPos = Midpoint(prev!.Position, next!.Position);
                    }
                    else if (prev != null)
                    {
                        newPos = prev.Position + STEP;
                    }
                    else if (next != null)
                    {
                        newPos = next.Position / 2;
                    }
                    else
                    {
                        throw new InvalidOperationException("Invalid prev/next task state after rebalance");
                    }

                    // Load task cần di chuyển
                    var task = await _context.Tasks
                        .FirstOrDefaultAsync(t => t.TaskId == taskId && !t.IsPendingDeleted)
                        ?? throw new InvalidOperationException($"Task {taskId} not found");

                    // Cập nhật vị trí + status (cho phép đổi cột)
                    task.Position = (int)newPos;
                    task.GroupStatusId = targetStatusId;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return;
                }
                catch (DbUpdateException)
                {
                    await transaction.RollbackAsync();

                    if (attemp < MAX_TRY)
                    {
                        await Task.Delay(50 * attemp);
                        continue;
                    }
                    throw;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            throw new InvalidOperationException("Failed to reorder task after maximum retries");
        }

        /// <summary>
        /// Rebalance public (có transaction riêng)
        /// </summary>
        public async Task RebalanceTasksInStatusAsync(Guid statusId)
        {
            using var transaction = await _context.Database
                .BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                await RebalanceTasksInStatusInternalAsync(statusId);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Tìm task kế tiếp sau position trong cùng status
        /// </summary>
        public async Task<TaskItem?> FindNextAfterAsync(Guid statusId, long position)
        {
            return await _context.Tasks
                .Where(t => t.GroupStatusId == statusId && t.Position > position && !t.IsPendingDeleted)
                .OrderBy(t => t.Position)
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        private async Task RebalanceTasksInStatusInternalAsync(Guid statusId)
        {
            var tasks = await _context.Tasks
                .Where(t => t.GroupStatusId == statusId && !t.IsPendingDeleted)
                .OrderBy(t => t.Position)
                .ToListAsync();

            long pos = STEP;
            foreach (var task in tasks)
            {
                task.Position = (int)pos;
                pos += STEP;
            }

            await _context.SaveChangesAsync();
        }

        private static long Midpoint(long a, long b)
        {
            return (a + b) / 2;
        }
    }
}