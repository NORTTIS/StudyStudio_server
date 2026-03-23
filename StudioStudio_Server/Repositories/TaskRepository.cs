using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
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
                .Where(t => t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value) && t.IsPendingDeleted == false)
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
            // Dung Progress>=100 lam dinh nghia "hoan thanh" (thong nhat voi GroupAnalyticsJob)
            int completedTasks = tasks.Count(t => t.Progress >= 100);
            int inProgressTasks = tasks.Count(t => t.Progress > 0 && t.Progress < 100);
            int notStartedTasks = tasks.Count(t => t.Progress == 0);
            // Qua han: co dueDate, da qua han, chua hoan thanh
            int overdueTasks = tasks.Count(t => t.DueDate.HasValue &&
                                                 t.DueDate.Value < now &&
                                                 t.Progress < 100);

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
                InProgressTasks = inProgressTasks,
                NotStartedTasks = notStartedTasks,
                CompletionPercentage = completionPercentage,
                OverdueTasks = overdueTasks,
                NearestDeadline = nearestDeadline,
                RiskFlags = riskFlags
            };
        }

        /// <summary>
        /// Add task to database
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
        /// Get list of soft deleted tasks in group
        /// Condition: GroupId = {groupId} AND IsPendingDeleted = true
        /// Order by: UpdatedAt ASC, Title DESC
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
        /// <summary>
        /// Get all personal tasks by personal status ID.
        /// Condition: PersonalStatusId = {statusId} AND GroupId IS NULL AND IsPendingDeleted = false.
        /// </summary>
        public async Task<List<TaskItem>> GetAllPersonalTasksByStatusIdAsync(Guid statusId)
        {
            return await _context.Tasks
                .Where(t => t.PersonalStatusId == statusId && !t.GroupId.HasValue && !t.IsPendingDeleted)
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

        public async Task<Dictionary<Guid, List<TaskItem>>> GetPersonalListTasksByListStatusId(List<Guid> listStatusIds)
        {
            Dictionary<Guid, List<TaskItem>> result = new Dictionary<Guid, List<TaskItem>>();

            foreach (var statusId in listStatusIds)
            {
                var tasks = await _context.Tasks
                    .Where(t => t.PersonalStatusId == statusId
                             && !t.GroupId.HasValue
                             && !t.IsPendingDeleted)
                    .AsNoTracking()
                    .OrderBy(t => t.Position)
                    .ToListAsync();

                result[statusId] = tasks;
            }

            return result;
        }

        /// <summary>
        /// Get personal tasks created by the user.
        /// Condition: OwnerId = {userId} AND GroupId IS NULL AND IsPendingDeleted = false.
        /// </summary>
        public async Task<List<TaskItem>> GetPersonalTasksByOwnerAsync(Guid userId)
        {
            return await _context.Tasks
                .Where(t => t.OwnerId == userId
                         && !t.GroupId.HasValue
                         && t.PersonalStatusId.HasValue
                         && !t.IsPendingDeleted)
                .Include(t => t.PersonalStatus)
                .AsNoTracking()
                .OrderBy(t => t.DueDate.HasValue ? 0 : 1)
                .ThenBy(t => t.DueDate)
                .ThenByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Get group tasks assigned to the user.
        /// Condition: Assignment.AssignedTo = {userId} AND GroupId IS NOT NULL AND IsPendingDeleted = false.
        /// </summary>
        public async Task<List<TaskItem>> GetAssignedGroupTasksByUserAsync(Guid userId)
        {
            var taskIds = await _context.TaskAssignments
                .Where(a => a.AssignedTo == userId)
                .Select(a => a.TaskId)
                .ToListAsync();

            if (!taskIds.Any())
            {
                return new List<TaskItem>();
            }

            return await _context.Tasks
                .Where(t => taskIds.Contains(t.TaskId) && t.GroupId.HasValue && !t.IsPendingDeleted)
                .Include(t => t.Group)
                .Include(t => t.GroupStatus)
                .AsNoTracking()
                .OrderBy(t => t.DueDate.HasValue ? 0 : 1)
                .ThenBy(t => t.DueDate)
                .ThenByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Get group tasks assigned to the user with pagination, search, filter, and sort.
        /// Applies filters and pagination at database level for better performance.
        /// </summary>
        public async Task<(List<TaskItem> Tasks, int TotalCount)> GetAssignedGroupTasksWithPaginationAsync(
            Guid userId,
            int page,
            int pageSize,
            string? search = null,
            Guid? groupId = null,
            bool sortAscending = true)
        {
            // Get task IDs assigned to user
            var taskIds = await _context.TaskAssignments
                .Where(a => a.AssignedTo == userId)
                .Select(a => a.TaskId)
                .ToListAsync();

            if (!taskIds.Any())
            {
                return (new List<TaskItem>(), 0);
            }

            // Build base query
            var query = _context.Tasks
                .Where(t => taskIds.Contains(t.TaskId) && t.GroupId.HasValue && !t.IsPendingDeleted)
                .Include(t => t.Group)
                .Include(t => t.GroupStatus)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(t =>
                    t.Title.Contains(search) ||
                    (t.Group != null && t.Group.GroupName.Contains(search)) ||
                    (t.GroupStatus != null && t.GroupStatus.StatusName.Contains(search))
                );
            }

            // Apply group filter
            if (groupId.HasValue)
            {
                query = query.Where(t => t.GroupId == groupId.Value);
            }

            // Get total count before pagination
            var totalCount = await query.CountAsync();

            // Apply sorting
            if (sortAscending)
            {
                query = query.OrderBy(t => t.DueDate.HasValue ? 0 : 1)
                            .ThenBy(t => t.DueDate);
            }
            else
            {
                query = query.OrderByDescending(t => t.DueDate.HasValue ? 0 : 1)
                            .ThenByDescending(t => t.DueDate);
            }

            // Apply pagination
            var tasks = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();

            return (tasks, totalCount);
        }

        /// <summary>
        /// Get group tasks with advanced filters, search, sort, and pagination at database level
        /// Supports filtering by: assignee, status, priority, severity, date ranges
        /// Supports search in: task title, description
        /// Supports sorting by: createdAt, dueDate, startDate, priority, severity, progress
        /// Applies all filters and pagination at database level for optimal performance
        /// </summary>
        public async Task<(List<TaskItem> Tasks, int TotalCount)> GetGroupTasksWithFiltersAsync(
            Guid groupId,
            int page,
            int pageSize,
            string? search = null,
            Guid? assigneeId = null,
            Guid? statusId = null,
            TaskPriority? priority = null,
            TaskSeverity? severity = null,
            DateTime? startDateFrom = null,
            DateTime? startDateTo = null,
            DateTime? dueDateFrom = null,
            DateTime? dueDateTo = null,
            string? sortBy = "createdAt",
            bool sortAscending = true)
        {
            // Convert DateTime parameters to UTC to avoid PostgreSQL timezone issues
            if (startDateFrom.HasValue && startDateFrom.Value.Kind == DateTimeKind.Unspecified)
            {
                startDateFrom = DateTime.SpecifyKind(startDateFrom.Value, DateTimeKind.Utc);
            }
            if (startDateTo.HasValue && startDateTo.Value.Kind == DateTimeKind.Unspecified)
            {
                startDateTo = DateTime.SpecifyKind(startDateTo.Value, DateTimeKind.Utc);
            }
            if (dueDateFrom.HasValue && dueDateFrom.Value.Kind == DateTimeKind.Unspecified)
            {
                dueDateFrom = DateTime.SpecifyKind(dueDateFrom.Value, DateTimeKind.Utc);
            }
            if (dueDateTo.HasValue && dueDateTo.Value.Kind == DateTimeKind.Unspecified)
            {
                dueDateTo = DateTime.SpecifyKind(dueDateTo.Value, DateTimeKind.Utc);
            }

            // Build base query
            var query = _context.Tasks
                .Where(t => t.GroupId == groupId && !t.IsPendingDeleted)
                .Include(t => t.GroupStatus)
                .Include(t => t.Owner)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(t =>
                    t.Title.Contains(search) ||
                    (t.Description != null && t.Description.Contains(search))
                );
            }

            // Apply assignee filter
            if (assigneeId.HasValue)
            {
                var assignedTaskIds = await _context.TaskAssignments
                    .Where(a => a.AssignedTo == assigneeId.Value)
                    .Select(a => a.TaskId)
                    .ToListAsync();

                query = query.Where(t => assignedTaskIds.Contains(t.TaskId));
            }

            // Apply status filter
            if (statusId.HasValue)
            {
                query = query.Where(t => t.GroupStatusId == statusId.Value);
            }

            // Apply priority filter
            if (priority.HasValue)
            {
                query = query.Where(t => t.Priority == priority.Value);
            }

            // Apply severity filter
            if (severity.HasValue)
            {
                query = query.Where(t => t.Severity == severity.Value);
            }

            // Apply start date range filter
            if (startDateFrom.HasValue)
            {
                query = query.Where(t => t.StartDate.HasValue && t.StartDate.Value >= startDateFrom.Value);
            }
            if (startDateTo.HasValue)
            {
                query = query.Where(t => t.StartDate.HasValue && t.StartDate.Value <= startDateTo.Value);
            }

            // Apply due date range filter
            if (dueDateFrom.HasValue)
            {
                query = query.Where(t => t.DueDate.HasValue && t.DueDate.Value >= dueDateFrom.Value);
            }
            if (dueDateTo.HasValue)
            {
                query = query.Where(t => t.DueDate.HasValue && t.DueDate.Value <= dueDateTo.Value);
            }

            // Get total count before pagination
            var totalCount = await query.CountAsync();

            // Apply sorting
            query = sortBy?.ToLower() switch
            {
                "duedate" => sortAscending
                    ? query.OrderBy(t => t.DueDate.HasValue ? 0 : 1).ThenBy(t => t.DueDate)
                    : query.OrderByDescending(t => t.DueDate.HasValue ? 0 : 1).ThenByDescending(t => t.DueDate),
                "startdate" => sortAscending
                    ? query.OrderBy(t => t.StartDate.HasValue ? 0 : 1).ThenBy(t => t.StartDate)
                    : query.OrderByDescending(t => t.StartDate.HasValue ? 0 : 1).ThenByDescending(t => t.StartDate),
                "priority" => sortAscending
                    ? query.OrderBy(t => t.Priority)
                    : query.OrderByDescending(t => t.Priority),
                "severity" => sortAscending
                    ? query.OrderBy(t => t.Severity)
                    : query.OrderByDescending(t => t.Severity),
                "progress" => sortAscending
                    ? query.OrderBy(t => t.Progress)
                    : query.OrderByDescending(t => t.Progress),
                "createdat" or _ => sortAscending
                    ? query.OrderBy(t => t.CreatedAt)
                    : query.OrderByDescending(t => t.CreatedAt)
            };

            // Apply pagination
            var tasks = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();

            return (tasks, totalCount);
        }

        /// <summary>
        /// Reorder (and change status if needed) for a task based on drag-and-drop position.
        /// Supports: dragging within same status, dragging to different status (including empty columns).
        /// </summary>
        public async Task ReorderTaskAsync(Guid taskId, Guid targetStatusId, Guid? prevTaskId, Guid? nextTaskId)
        {
            for (int attemp = 1; attemp <= MAX_TRY; attemp++)
            {
                using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

                try
                {
                    var targetStatus = await _context.GroupTaskStatuses
                        .FirstOrDefaultAsync(s => s.StatusId == targetStatusId && !s.IsDeleted)
                    ?? throw new InvalidOperationException($"Status {targetStatusId} not found");

                    // Load task to be moved first
                    var task = await _context.Tasks
                        .FirstOrDefaultAsync(t => t.TaskId == taskId && !t.IsPendingDeleted)
                        ?? throw new InvalidOperationException($"Task {taskId} not found");

                    // Load prev/next task (must belong to targetStatus and exclude the moving task)
                    var prev = prevTaskId.HasValue
                        ? await _context.Tasks
                            .FirstOrDefaultAsync(t => t.TaskId == prevTaskId.Value
                                                   && t.GroupStatusId == targetStatusId
                                                   && t.TaskId != taskId
                                                   && !t.IsPendingDeleted)
                        : null;

                    var next = nextTaskId.HasValue
                        ? await _context.Tasks
                            .FirstOrDefaultAsync(t => t.TaskId == nextTaskId.Value
                                                   && t.GroupStatusId == targetStatusId
                                                   && t.TaskId != taskId
                                                   && !t.IsPendingDeleted)
                        : null;

                    long newPos;

                    // Case 1: Moving to empty column (both prev and next are null)
                    if (prev == null && next == null)
                    {
                        // Check if there are other tasks in the target status (excluding the moving task)
                        var existingTasks = await _context.Tasks
                            .Where(t => t.GroupStatusId == targetStatusId
                                     && t.TaskId != taskId
                                     && !t.IsPendingDeleted)
                            .OrderBy(t => t.Position)
                            .ToListAsync();

                        if (existingTasks.Any())
                        {
                            // Place at the end
                            newPos = existingTasks.Max(t => t.Position) + STEP;
                        }
                        else
                        {
                            // Truly empty column
                            newPos = STEP;
                        }
                    }
                    // Case 2: Between two tasks
                    else if (prev != null && next != null)
                    {
                        long gap = next.Position - prev.Position;

                        if (gap <= 1)
                        {
                            await RebalanceTasksInStatusInternalAsync(targetStatusId);

                            // Reload after rebalance
                            prev = await _context.Tasks
                                .FirstOrDefaultAsync(t => t.TaskId == prevTaskId!.Value && !t.IsPendingDeleted);
                            next = await _context.Tasks
                                .FirstOrDefaultAsync(t => t.TaskId == nextTaskId!.Value && !t.IsPendingDeleted);
                        }

                        newPos = Midpoint(prev!.Position, next!.Position);
                    }
                    // Case 3: After last task (only prev exists)
                    else if (prev != null)
                    {
                        newPos = prev.Position + STEP;
                    }
                    // Case 4: Before first task (only next exists)
                    else if (next != null)
                    {
                        newPos = next.Position / 2;

                        // If calculated position is too small, rebalance and recalculate
                        if (newPos < 1)
                        {
                            await RebalanceTasksInStatusInternalAsync(targetStatusId);

                            next = await _context.Tasks
                                .FirstOrDefaultAsync(t => t.TaskId == nextTaskId!.Value && !t.IsPendingDeleted);


                            newPos = next!.Position / 2;
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException("Invalid prev/next task state after rebalance");
                    }

                    // Update position + status (allows changing column)
                    task.Position = (int)newPos;
                    task.GroupStatusId = targetStatusId;
                    task.UpdatedAt = DateTime.UtcNow;

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
        /// Rebalance all tasks in status (public method with separate transaction)
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
        /// Find next task after position in same status
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


        /// <summary>
        /// Reorder (and change status if needed) for a task based on drag-and-drop position.
        /// Supports: dragging within same status, dragging to different status (including empty columns).
        /// </summary>
        public async Task ReorderPersonalTaskAsync(Guid taskId, Guid targetStatusId, Guid? prevTaskId, Guid? nextTaskId)
        {
            for (int attemp = 1; attemp <= MAX_TRY; attemp++)
            {
                using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

                try
                {
                    var targetStatus = await _context.PersonalTaskStatuses
                        .FirstOrDefaultAsync(s => s.StatusId == targetStatusId)
                    ?? throw new InvalidOperationException($"Status {targetStatusId} not found");

                    // Load task to be moved first
                    var task = await _context.Tasks
                        .FirstOrDefaultAsync(t => t.TaskId == taskId && !t.IsPendingDeleted)
                        ?? throw new InvalidOperationException($"Task {taskId} not found");

                    // Load prev/next task (must belong to targetStatus and exclude the moving task)
                    var prev = prevTaskId.HasValue
                        ? await _context.Tasks
                            .FirstOrDefaultAsync(t => t.TaskId == prevTaskId.Value
                                                   && t.PersonalStatusId == targetStatusId
                                                   && t.TaskId != taskId
                                                   && !t.IsPendingDeleted)
                        : null;

                    var next = nextTaskId.HasValue
                        ? await _context.Tasks
                            .FirstOrDefaultAsync(t => t.TaskId == nextTaskId.Value
                                                   && t.PersonalStatusId == targetStatusId
                                                   && t.TaskId != taskId
                                                   && !t.IsPendingDeleted)
                        : null;

                    long newPos;

                    // Case 1: Moving to empty column (both prev and next are null)
                    if (prev == null && next == null)
                    {
                        // Check if there are other tasks in the target status (excluding the moving task)
                        var existingTasks = await _context.Tasks
                            .Where(t => t.PersonalStatusId == targetStatusId
                                     && t.TaskId != taskId
                                     && !t.IsPendingDeleted)
                            .OrderBy(t => t.Position)
                            .ToListAsync();

                        if (existingTasks.Any())
                        {
                            // Place at the end
                            newPos = existingTasks.Max(t => t.Position) + STEP;
                        }
                        else
                        {
                            // Truly empty column
                            newPos = STEP;
                        }
                    }
                    // Case 2: Between two tasks
                    else if (prev != null && next != null)
                    {
                        long gap = next.Position - prev.Position;

                        if (gap <= 1)
                        {
                            await RebalancePersonalTasksInStatusInternalAsync(targetStatusId);

                            // Reload after rebalance
                            prev = await _context.Tasks
                                .FirstOrDefaultAsync(t => t.TaskId == prevTaskId!.Value && !t.IsPendingDeleted);
                            next = await _context.Tasks
                                .FirstOrDefaultAsync(t => t.TaskId == nextTaskId!.Value && !t.IsPendingDeleted);
                        }

                        newPos = Midpoint(prev!.Position, next!.Position);
                    }
                    // Case 3: After last task (only prev exists)
                    else if (prev != null)
                    {
                        newPos = prev.Position + STEP;
                    }
                    // Case 4: Before first task (only next exists)
                    else if (next != null)
                    {
                        newPos = next.Position / 2;

                        // If calculated position is too small, rebalance and recalculate
                        if (newPos < 1)
                        {
                            await RebalancePersonalTasksInStatusInternalAsync(targetStatusId);

                            next = await _context.Tasks
                                .FirstOrDefaultAsync(t => t.TaskId == nextTaskId!.Value && !t.IsPendingDeleted);


                            newPos = next!.Position / 2;
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException("Invalid prev/next task state after rebalance");
                    }

                    // Update position + status (allows changing column)
                    task.Position = (int)newPos;
                    task.PersonalStatusId = targetStatusId;
                    task.UpdatedAt = DateTime.UtcNow;

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
        /// Rebalance all tasks in status (public method with separate transaction)
        /// </summary>
        public async Task RebalancePersonalTasksInStatusAsync(Guid statusId)
        {
            using var transaction = await _context.Database
                .BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                await RebalancePersonalTasksInStatusInternalAsync(statusId);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Find next task after position in same status
        /// </summary>
        public async Task<TaskItem?> PersonalFindNextAfterAsync(Guid statusId, long position)
        {
            return await _context.Tasks
                .Where(t => t.PersonalStatusId == statusId && t.Position > position && !t.IsPendingDeleted)
                .OrderBy(t => t.Position)
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        private async Task RebalancePersonalTasksInStatusInternalAsync(Guid statusId)
        {
            var tasks = await _context.Tasks
                .Where(t => t.PersonalStatusId == statusId && !t.IsPendingDeleted)
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

        /// <summary>
        /// Permanent delete task from database (hard delete)
        /// Also deletes related ActivityLog and TaskAssignment records
        /// </summary>
        public async Task PermanentDeleteAsync(Guid taskId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var activityLogs = await _context.ActivityLogs
                    .Where(h => h.TargetId == taskId && h.TargetType == "TASK")
                    .ToListAsync();
                if (activityLogs.Any())
                {
                    _context.ActivityLogs.RemoveRange(activityLogs);
                }

                var taskAssignments = await _context.TaskAssignments
                    .Where(a => a.TaskId == taskId)
                    .ToListAsync();
                if (taskAssignments.Any())
                {
                    _context.TaskAssignments.RemoveRange(taskAssignments);
                }

                var task = await _context.Tasks
                    .FirstOrDefaultAsync(t => t.TaskId == taskId);
                if (task != null)
                {
                    _context.Tasks.Remove(task);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}