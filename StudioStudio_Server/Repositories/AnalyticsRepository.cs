using System.Globalization;
using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository for analytics data operations
    /// </summary>
    public class AnalyticsRepository(StudioDbContext context) : IAnalyticsRepository
    {
        // ==================== GROUP ANALYTICS ====================

        public async Task<List<GroupAnalytics>> GetGroupAnalyticsRangeAsync(Guid groupId, DateOnly startDate, DateOnly endDate)
        {
            return await context.GroupAnalytics
                .AsNoTracking()
                .Where(x => x.GroupId == groupId && x.Date >= startDate && x.Date <= endDate)
                .OrderBy(x => x.Date)
                .ToListAsync();
        }

        public async Task<Dictionary<Guid, List<GroupAnalytics>>> GetGroupAnalyticsRangeBatchAsync(List<Guid> groupIds, DateOnly startDate, DateOnly endDate)
        {
            var all = await context.GroupAnalytics
                .AsNoTracking()
                .Where(x => groupIds.Contains(x.GroupId) && x.Date >= startDate && x.Date <= endDate)
                .OrderBy(x => x.Date)
                .ToListAsync();

            return all.GroupBy(x => x.GroupId).ToDictionary(g => g.Key, g => g.ToList());
        }

        public async Task UpsertGroupAnalyticsAsync(GroupAnalytics analytics)
        {
            var existing = await context.GroupAnalytics
                .FirstOrDefaultAsync(x => x.GroupId == analytics.GroupId && x.Date == analytics.Date);

            if (existing != null)
            {
                existing.TotalTasks = analytics.TotalTasks;
                existing.CompletedTasks = analytics.CompletedTasks;
                existing.OverdueTasks = analytics.OverdueTasks;
                existing.ActiveMembers = analytics.ActiveMembers;
                existing.MessagesCount = analytics.MessagesCount;
                existing.CommentsCount = analytics.CommentsCount;
                existing.CompletionRate = analytics.CompletionRate;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                analytics.CreatedAt = DateTime.UtcNow;
                analytics.UpdatedAt = DateTime.UtcNow;
                context.GroupAnalytics.Add(analytics);
            }

            await context.SaveChangesAsync();
        }

        // ==================== AGGREGATION HELPERS FOR ETL JOBS ====================

        public async Task<Dictionary<Guid, int>> AggregateTasksByGroupAsync(Guid groupId, DateTime from, DateTime to)
        {
            return await context.Tasks
                .Where(t => t.GroupId == groupId && t.CreatedAt >= from && t.CreatedAt <= to)
                .GroupBy(t => t.GroupId!.Value)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.GroupId, x => x.Count);
        }

        public async Task<Dictionary<Guid, int>> AggregateCompletedTasksByGroupAsync(Guid groupId, DateTime from, DateTime to)
        {
            return await context.Tasks
                .Where(t => t.GroupId == groupId && t.CompletedAt.HasValue && t.CompletedAt >= from && t.CompletedAt <= to)
                .GroupBy(t => t.GroupId!.Value)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.GroupId, x => x.Count);
        }

        // ==================== GROUP ANALYTICS ENHANCED ====================

        /// <summary>
        /// Get task status counts (done, in-progress, todo, overdue) per member in a group within date range
        /// Done: Progress == 100 OR CompletedAt is not null
        /// In-Progress: Progress > 0 AND Progress < 100 AND DueDate >= now
        /// Todo: Progress == 0 AND (no due date OR due date >= now)
        /// Overdue: DueDate < now AND Progress < 100
        /// </summary>
        public async Task<Dictionary<Guid, (int Done, int InProgress, int Todo, int Overdue, int InProgressOverdue, int TodoOverdue, int Total)>> GetMemberTaskStatusBreakdownAsync(
            Guid groupId, DateTime from, DateTime to)
        {
            return await GetMemberTaskStatusBreakdownAsync(context, groupId, from, to);
        }

        public async Task<Dictionary<Guid, (int Done, int InProgress, int Todo, int Overdue, int InProgressOverdue, int TodoOverdue, int Total)>> GetMemberTaskStatusBreakdownAsync(
            StudioDbContext context, Guid groupId, DateTime from, DateTime to)
        {
            // Get all tasks in the group
            var tasks = await context.Tasks
                .AsNoTracking()
                .Where(t => t.GroupId == groupId && t.CreatedAt >= from && t.CreatedAt <= to)
                .Select(t => new
                {
                    t.TaskId,
                    t.Progress,
                    t.CompletedAt,
                    t.DueDate,
                    OwnerId = t.OwnerId
                })
                .ToListAsync();

            // Get all task assignments for tasks in this group
            var taskIds = tasks.Select(t => t.TaskId).ToList();
            var assignments = await context.TaskAssignments
                .AsNoTracking()
                .Where(a => taskIds.Contains(a.TaskId))
                .Select(a => new { a.TaskId, a.AssignedTo })
                .ToListAsync();

            // Create lookup: TaskId -> List of AssigneeIds
            var assigneeLookup = assignments
                .GroupBy(a => a.TaskId)
                .ToDictionary(g => g.Key, g => g.Select(a => a.AssignedTo).ToList());

            // Get all members in the group
            var memberIds = await context.GroupParticipants
                .Where(p => p.GroupId == groupId)
                .Select(p => p.UserId)
                .ToListAsync();

            // For each member, count tasks where they are assigned OR they are the owner
            var result = new Dictionary<Guid, (int Done, int InProgress, int Todo, int Overdue, int InProgressOverdue, int TodoOverdue, int Total)>();

            foreach (var memberId in memberIds)
            {
                var memberTasks = tasks
                    .Where(t => assigneeLookup.TryGetValue(t.TaskId, out var assignees) && assignees.Contains(memberId))
                    .ToList();

                var done = memberTasks.Count(t => t.Progress == 100 || t.CompletedAt != null);
                var overdue = memberTasks.Count(t =>
                    t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow && t.Progress < 100);
                var inProgress = memberTasks.Count(t => t.Progress > 0 && t.Progress < 100);
                var todo = memberTasks.Count(t => t.Progress == 0);
                // Intersection counts for Venn diagram
                var inProgressOverdue = memberTasks.Count(t =>
                    t.Progress > 0 && t.Progress < 100 &&
                    t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow);
                var todoOverdue = memberTasks.Count(t =>
                    t.Progress == 0 &&
                    t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow);
                var total = memberTasks.Count;

                result[memberId] = (done, inProgress, todo, overdue, inProgressOverdue, todoOverdue, total);
            }

            return result;
        }

        /// <summary>
        /// Get daily completed tasks count per member within date range
        /// </summary>
        public async Task<Dictionary<Guid, Dictionary<DateOnly, int>>> GetMemberDailyCompletionsAsync(
            Guid groupId, DateOnly startDate, DateOnly endDate)
        {
            return await GetMemberDailyCompletionsAsync(context, groupId, startDate, endDate);
        }

        public async Task<Dictionary<Guid, Dictionary<DateOnly, int>>> GetMemberDailyCompletionsAsync(
            StudioDbContext context, Guid groupId, DateOnly startDate, DateOnly endDate)
        {
            // User inputs local dates, DB stores TIMESTAMPTZ (UTC).
            // Convert local date range to UTC for DB query.
            var zoneId = TimeZoneInfo.TryConvertIanaIdToWindowsId("Asia/Bangkok", out var windowsId)
                ? windowsId
                : "SE Asia Standard Time";
            var tz = TimeZoneInfo.FindSystemTimeZoneById(zoneId);

            DateTime ToUtcStart(DateOnly d) => TimeZoneInfo.ConvertTimeToUtc(d.ToDateTime(TimeOnly.MinValue), tz);
            DateTime ToUtcEnd(DateOnly d) => TimeZoneInfo.ConvertTimeToUtc(d.ToDateTime(TimeOnly.MaxValue), tz);

            var startDateTime = DateTime.SpecifyKind(ToUtcStart(startDate), DateTimeKind.Utc);
            var endDateTime = DateTime.SpecifyKind(ToUtcEnd(endDate), DateTimeKind.Utc);

            // Helper: convert UTC timestamp from DB → local DateOnly
            DateOnly ToLocalDate(DateTime utcDt) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utcDt, DateTimeKind.Utc), tz));

            var rawCompleted = await context.Tasks
                .AsNoTracking()
                .Where(t => t.GroupId == groupId
                            && t.CompletedAt.HasValue
                            && t.CompletedAt >= startDateTime
                            && t.CompletedAt <= endDateTime)
                .Select(t => new { t.TaskId, t.OwnerId, t.CompletedAt })
                .ToListAsync();

            var taskIds = rawCompleted.Select(t => t.TaskId).Distinct().ToList();
            var assignments = await context.TaskAssignments
                .AsNoTracking()
                .Where(a => taskIds.Contains(a.TaskId))
                .Select(a => new { a.TaskId, a.AssignedTo })
                .ToListAsync();

            var assigneeLookup = assignments
                .GroupBy(a => a.TaskId)
                .ToDictionary(g => g.Key, g => g.Select(a => a.AssignedTo).ToList());

            var completedTasks = new List<(Guid UserId, DateOnly CompletedDate)>();
            foreach (var task in rawCompleted)
            {
                var completedDate = ToLocalDate(task.CompletedAt!.Value);
                if (assigneeLookup.TryGetValue(task.TaskId, out var assignees) && assignees.Count > 0)
                {
                    foreach (var assignee in assignees)
                        completedTasks.Add((assignee, completedDate));
                }
                else
                {
                    completedTasks.Add((task.OwnerId, completedDate));
                }
            }

            var result = completedTasks
                .GroupBy(t => t.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .GroupBy(t => t.CompletedDate)
                        .ToDictionary(g2 => g2.Key, g2 => g2.Count()));

            // Ensure all members have entries for all dates (fill with 0)
            var userIds = completedTasks.Select(t => t.UserId).Distinct().ToList();
            foreach (var userId in userIds)
            {
                if (!result.ContainsKey(userId))
                    result[userId] = new Dictionary<DateOnly, int>();

                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    if (!result[userId].ContainsKey(date))
                        result[userId][date] = 0;
                }
            }

            return result;
        }

        /// <summary>
        /// Get last activity datetime per member in a group
        /// Activity = task completion, message sent, or comment posted
        /// </summary>
        public async Task<Dictionary<Guid, DateTime?>> GetMemberLastActivityAsync(Guid groupId)
        {
            return await GetMemberLastActivityAsync(context, groupId);
        }

        public async Task<Dictionary<Guid, DateTime?>> GetMemberLastActivityAsync(StudioDbContext context, Guid groupId)
        {
            var participantIds = await context.GroupParticipants
                .Where(p => p.GroupId == groupId)
                .Select(p => p.UserId)
                .ToListAsync();

            // Get last task completion
            var lastTaskCompletion = await context.Tasks
                .Where(t => t.GroupId == groupId && t.CompletedAt.HasValue)
                .GroupBy(t => t.OwnerId)
                .Select(g => new { UserId = g.Key, LastDate = g.Max(t => t.CompletedAt) })
                .ToDictionaryAsync(x => x.UserId, x => (DateTime?)x.LastDate);

            // Get last message
            var lastMessage = await context.GroupMessages
                .Where(m => m.GroupId == groupId)
                .GroupBy(m => m.UserId)
                .Select(g => new { UserId = g.Key, LastDate = g.Max(m => m.CreatedAt) })
                .ToDictionaryAsync(x => x.UserId, x => (DateTime?)x.LastDate);

            // Get last comment
            var lastComment = await context.TaskComments
                .Where(c => c.Task.GroupId == groupId)
                .GroupBy(c => c.UserId)
                .Select(g => new { UserId = g.Key, LastDate = g.Max(c => c.CreatedAt) })
                .ToDictionaryAsync(x => x.UserId, x => (DateTime?)x.LastDate);

            var result = new Dictionary<Guid, DateTime?>();
            foreach (var userId in participantIds)
            {
                var taskDate = lastTaskCompletion.GetValueOrDefault(userId);
                var messageDate = lastMessage.GetValueOrDefault(userId);
                var commentDate = lastComment.GetValueOrDefault(userId);

                var latest = new[] { taskDate, messageDate, commentDate }
                    .Where(d => d.HasValue)
                    .OrderByDescending(d => d)
                    .FirstOrDefault();

                result[userId] = latest;
            }

            return result;
        }

        /// <summary>
        /// Get task status counts per member WITHOUT date filter (all time) - for summary endpoint
        /// Counts tasks where user is assigned OR is the owner
        /// </summary>
        public async Task<Dictionary<Guid, (int Done, int InProgress, int Todo, int Overdue, int InProgressOverdue, int TodoOverdue, int Total)>> GetMemberTaskStatusBreakdownAllTimeAsync(Guid groupId)
        {
            // Get all tasks in the group
            var tasks = await context.Tasks
                .AsNoTracking()
                .Where(t => t.GroupId == groupId && t.IsPendingDeleted == false)
                .Select(t => new
                {
                    t.TaskId,
                    t.Progress,
                    t.CompletedAt,
                    t.DueDate,
                    t.OwnerId
                })
                .ToListAsync();

            // Get all task assignments for tasks in this group
            var taskIds = tasks.Select(t => t.TaskId).ToList();
            var assignments = await context.TaskAssignments
                .AsNoTracking()
                .Where(a => taskIds.Contains(a.TaskId))
                .Select(a => new { a.TaskId, a.AssignedTo })
                .ToListAsync();

            // Create a lookup: TaskId -> List of AssigneeIds
            var assigneeLookup = assignments
                .GroupBy(a => a.TaskId)
                .ToDictionary(g => g.Key, g => g.Select(a => a.AssignedTo).ToList());

            // Get all members in the group
            var memberIds = await context.GroupParticipants
                .Where(p => p.GroupId == groupId)
                .Select(p => p.UserId)
                .ToListAsync();

            // For each member, count tasks where they are assigned OR they are the owner
            var result = new Dictionary<Guid, (int Done, int InProgress, int Todo, int Overdue, int InProgressOverdue, int TodoOverdue, int Total)>();

            foreach (var memberId in memberIds)
            {
                var memberTasks = tasks
                    .Where(t => assigneeLookup.TryGetValue(t.TaskId, out var assignees) && assignees.Contains(memberId))
                    .ToList();

                var done = memberTasks.Count(t => t.Progress == 100 || t.CompletedAt != null);
                var overdue = memberTasks.Count(t =>
                    t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow && t.Progress < 100);
                var inProgress = memberTasks.Count(t => t.Progress > 0 && t.Progress < 100);
                var todo = memberTasks.Count(t => t.Progress == 0);
                // Intersection counts for Venn diagram
                var inProgressOverdue = memberTasks.Count(t =>
                    t.Progress > 0 && t.Progress < 100 &&
                    t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow);
                var todoOverdue = memberTasks.Count(t =>
                    t.Progress == 0 &&
                    t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow);
                var total = memberTasks.Count;

                result[memberId] = (done, inProgress, todo, overdue, inProgressOverdue, todoOverdue, total);
            }

            return result;
        }


        public async Task<List<double>> GetUserPersonalTaskCompletionTimesAsync(Guid userId)
        {
            var completed = await context.Tasks
                .AsNoTracking()
                .Where(t => t.OwnerId == userId && t.GroupId == null && t.CompletedAt.HasValue)
                .Select(t => new { t.CreatedAt, t.CompletedAt })
                .ToListAsync();

            return completed
                .Where(t => t.CompletedAt.HasValue)
                .Select(t => (t.CompletedAt!.Value - t.CreatedAt).TotalDays)
                .ToList();
        }

        public async Task<Dictionary<Guid, double>> GetUserGroupActivityScoresAsync(List<Guid> groupIds, Guid userId, DateTime? from = null, DateTime? to = null)
        {
            // Get user's activity from ActivityLogs in their groups
            var query = context.ActivityLogs
                .AsNoTracking()
                .Where(a => a.GroupId.HasValue && groupIds.Contains(a.GroupId.Value) && a.UserId == userId);

            if (from.HasValue)
                query = query.Where(a => a.CreatedAt >= from.Value);
            if (to.HasValue)
                query = query.Where(a => a.CreatedAt < to.Value.AddDays(1));

            var activityLogs = await query
                .Select(a => new { GroupId = a.GroupId, CreatedAt = a.CreatedAt, ActionType = a.ActionType, TaskPriority = a.TaskPriority ?? 0, TaskSeverity = a.TaskSeverity ?? 0 })
                .ToListAsync();

            return activityLogs
                .GroupBy(a => a.GroupId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(a =>
                        ActivityScoreHelper.GetScore(a.ActionType, a.TaskPriority, a.TaskSeverity)));
        }

        /// <summary>
        /// Get activity scores for ALL members in the given groups (all time).
        /// Returns: Dictionary&lt;GroupId, Dictionary&lt;UserId, TotalScore&gt;&gt;
        ///
        /// Scoring rules:
        /// - TASK_COMPLETE: credits points to the ASSIGNED user(s) via TaskAssignment (not the completer).
        ///   If a task has multiple assignees, each receives full points.
        ///   If no assignment exists, credits the completer (UserId) as fallback.
        /// - All other action types: credits the UserId who performed the action.
        /// </summary>
        public async Task<Dictionary<Guid, Dictionary<Guid, double>>> GetAllMembersGroupActivityScoresAsync(
            List<Guid> groupIds, DateTime? from = null, DateTime? to = null)
        {
            var activityLogs = await context.ActivityLogs
                .AsNoTracking()
                .Where(a => a.GroupId.HasValue && groupIds.Contains(a.GroupId.Value)
                    && a.ActionType != "MESSAGE_CREATE") // Messages counted via GroupMessages table
                .Select(a => new
                {
                    GroupId = a.GroupId!.Value,
                    UserId = a.UserId,
                    TargetId = a.TargetId,
                    CreatedAt = a.CreatedAt,
                    ActionType = a.ActionType,
                    TaskPriority = a.TaskPriority ?? 0,
                    TaskSeverity = a.TaskSeverity ?? 0
                })
                .ToListAsync();

            if (from.HasValue)
                activityLogs = activityLogs.Where(a => a.CreatedAt >= from.Value).ToList();
            if (to.HasValue)
                activityLogs = activityLogs.Where(a => a.CreatedAt < to.Value.AddDays(1)).ToList();

            // Pre-fetch all task assignments for tasks that belong to these groups
            var taskIds = activityLogs
                .Where(a => a.TargetId.HasValue)
                .Select(a => a.TargetId!.Value)
                .Distinct()
                .ToList();

            var assignments = await context.TaskAssignments
                .AsNoTracking()
                .Where(a => taskIds.Contains(a.TaskId))
                .Select(a => new { a.TaskId, a.AssignedTo })
                .ToListAsync();

            var assigneesByTask = assignments
                .GroupBy(a => a.TaskId)
                .ToDictionary(g => g.Key, g => g.Select(a => a.AssignedTo).ToList());

            // Expand TASK_COMPLETE logs: one entry per assignee (or per completer if no assignment)
            var expandedLogs = new List<(Guid GroupId, Guid UserId, string ActionType, int TaskPriority, int TaskSeverity)>();

            foreach (var log in activityLogs)
            {
                if (log.ActionType == "TASK_COMPLETE" && log.TargetId.HasValue)
                {
                    if (assigneesByTask.TryGetValue(log.TargetId.Value, out var assignees) && assignees.Count > 0)
                    {
                        // Credit each assignee
                        foreach (var assignee in assignees)
                            expandedLogs.Add((log.GroupId, assignee, log.ActionType, log.TaskPriority, log.TaskSeverity));
                    }
                    else
                    {
                        // No assignment → credit the completer
                        expandedLogs.Add((log.GroupId, log.UserId, log.ActionType, log.TaskPriority, log.TaskSeverity));
                    }
                }
                else
                {
                    expandedLogs.Add((log.GroupId, log.UserId, log.ActionType, log.TaskPriority, log.TaskSeverity));
                }
            }

            return expandedLogs
                .GroupBy(a => a.GroupId)
                .ToDictionary(
                    g => g.Key,
                    g => g.GroupBy(a => a.UserId)
                         .ToDictionary(
                            u => u.Key,
                            u => u.Sum(a => ActivityScoreHelper.GetScore(a.ActionType, a.TaskPriority, a.TaskSeverity))));
        }

        /// <summary>
        /// Get per-member total scores and messages for a SINGLE group (all time).
        /// Scoring: ActivityScoreHelper with assignee credit for TASK_COMPLETE.
        /// Messages: from GroupMessages table (not ActivityLogs).
        /// </summary>
        public async Task<Dictionary<Guid, MemberContributionResult>> GetGroupMemberScoresAsync(Guid groupId)
        {
            // Get activity scores via the existing multi-group method
            var groupScores = await GetAllMembersGroupActivityScoresAsync(new List<Guid> { groupId });
            var scores = groupScores.GetValueOrDefault(groupId, new Dictionary<Guid, double>());

            // Get messages from GroupMessages (all time)
            var messagesByUser = await context.GroupMessages
                .AsNoTracking()
                .Where(m => m.GroupId == groupId)
                .GroupBy(m => m.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            return scores.ToDictionary(
                kvp => kvp.Key,
                kvp => new MemberContributionResult
                {
                    UserId = kvp.Key,
                    TotalScore = kvp.Value + messagesByUser.GetValueOrDefault(kvp.Key, 0),
                    MessagesSent = messagesByUser.GetValueOrDefault(kvp.Key, 0)
                });
        }

        public async Task<List<(int Priority, int Done, int InProgress, int Overdue, int Todo, int Total)>> GetUserTasksByPriorityAsync(
            List<Guid> groupIds, Guid userId)
        {
            // Get personal tasks + all group tasks where user is owner
            var personalTasks = await context.Tasks
                .AsNoTracking()
                .Where(t => t.OwnerId == userId && t.GroupId == null)
                .Select(t => new {
                    Priority = (int)t.Priority,
                    IsDone = t.Progress == 100 || t.CompletedAt != null,
                    IsOverdue = t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow && t.Progress < 100,
                    IsInProgress = t.Progress > 0 && t.Progress < 100 && (!t.DueDate.HasValue || t.DueDate.Value >= DateTime.UtcNow)
                })
                .ToListAsync();

            var groupTasks = await context.Tasks
                .AsNoTracking()
                .Where(t => t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value)
                    && t.IsPendingDeleted == false
                    && context.TaskAssignments.Any(a => a.TaskId == t.TaskId && a.AssignedTo == userId))
                .Select(t => new {
                    Priority = (int)t.Priority,
                    IsDone = t.Progress == 100 || t.CompletedAt != null,
                    IsOverdue = t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow && t.Progress < 100,
                    IsInProgress = t.Progress > 0 && t.Progress < 100 && (!t.DueDate.HasValue || t.DueDate.Value >= DateTime.UtcNow)
                })
                .ToListAsync();

            var all = personalTasks.Concat(groupTasks).ToList();

            return all
                .GroupBy(t => t.Priority)
                .Select(g =>
                {
                    var done = g.Count(t => t.IsDone);
                    var overdue = g.Count(t => t.IsOverdue);
                    var inProgress = g.Count(t => t.IsInProgress && !t.IsDone && !t.IsOverdue);
                    var todo = g.Count(t => !t.IsDone && !t.IsInProgress && !t.IsOverdue);
                    var total = g.Count();
                    var priority = g.Key;
                    return (priority, done, inProgress, overdue, todo, total);
                })
                .OrderByDescending(x => x.priority)
                .ToList();
        }

        public async Task<List<(Guid? GroupId, Guid? TaskId, string Title, string? GroupName, DateTime DueDate)>> GetUserOverdueTasksAsync(
            List<Guid> groupIds, Guid userId, int limit = 10)
        {
            var now = DateTime.UtcNow;

            var personal = await context.Tasks
                .AsNoTracking()
                .Where(t => t.OwnerId == userId && t.GroupId == null && t.DueDate < now && t.Progress < 100)
                .Select(t => new {
                    t.GroupId,
                    t.TaskId,
                    t.Title,
                    GroupName = (string?)null,
                    t.DueDate
                })
                .Take(limit)
                .ToListAsync();

            var group = await context.Tasks
                .AsNoTracking()
                .Where(t => t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value)
                    && t.IsPendingDeleted == false
                    && context.TaskAssignments.Any(a => a.TaskId == t.TaskId && a.AssignedTo == userId)
                    && t.DueDate < now && t.Progress < 100)
                .Select(t => new {
                    t.GroupId,
                    t.TaskId,
                    t.Title,
                    t.Group!.GroupName,
                    t.DueDate
                })
                .Take(limit)
                .ToListAsync();

            var combined = personal
                .Concat(group)
                .Take(limit)
                .Select(x => new { x.GroupId, x.TaskId, x.Title, x.GroupName, x.DueDate })
                .ToList();

            return combined
                .Where(x => x.DueDate.HasValue)
                .Select(x => (x.GroupId, (Guid?)x.TaskId, x.Title, x.GroupName!, x.DueDate!.Value))
                .ToList();
        }


        public async Task<List<(int Year, int Week, int Score, int Count)>> GetUserWeeklyScoresAsync(
            List<Guid> groupIds, Guid userId, int weeks = 7)
        {
            var startDate = DateTime.UtcNow.AddDays(-7 * weeks);

            var activityLogs = await context.ActivityLogs
                .AsNoTracking()
                .Where(a => a.GroupId.HasValue && groupIds.Contains(a.GroupId.Value) && a.UserId == userId && a.CreatedAt >= startDate)
                .Select(a => new {
                    CreatedAt = a.CreatedAt,
                    ActionType = a.ActionType,
                    TaskPriority = a.TaskPriority ?? 0,
                    TaskSeverity = a.TaskSeverity ?? 0
                })
                .ToListAsync();

            // Single query to get messages (replaces N+1 Task.WhenAll)
            var allMessages = await context.GroupMessages
                .AsNoTracking()
                .Where(m => groupIds.Contains(m.GroupId) && m.UserId == userId && m.CreatedAt >= startDate)
                .Select(m => new { m.CreatedAt })
                .ToListAsync();

            return activityLogs
                .Select(a =>
                {
                    var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
                    var year = cal.GetYear(a.CreatedAt);
                    var week = cal.GetWeekOfYear(a.CreatedAt, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                    var score = ActivityScoreHelper.GetScore(a.ActionType, a.TaskPriority, a.TaskSeverity);
                    return (Year: year, Week: week, Score: score, Count: 1);
                })
                .Concat(allMessages.Select(m =>
                {
                    var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
                    var year = cal.GetYear(m.CreatedAt);
                    var week = cal.GetWeekOfYear(m.CreatedAt, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                    return (Year: year, Week: week, Score: 1.0, Count: 1);
                }))
                .GroupBy(x => (x.Year, x.Week))
                .Select(g => (g.Key.Year, g.Key.Week, Score: (int)g.Sum(x => x.Score), Count: g.Count()))
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Week)
                .ToList();
        }

        public async Task<double?> GetGroupAvgWeeklyScoreAsync(Guid groupId, int year, int week)
        {
            var weekStart = DateTime.SpecifyKind(ISOWeek.ToDateTime(year, week, DayOfWeek.Monday), DateTimeKind.Utc);
            var weekEnd = DateTime.SpecifyKind(weekStart.AddDays(7), DateTimeKind.Utc);

            var memberIds = await context.GroupParticipants
                .Where(p => p.GroupId == groupId)
                .Select(p => p.UserId)
                .ToListAsync();

            if (!memberIds.Any()) return null;

            var activityLogs = await context.ActivityLogs
                .AsNoTracking()
                .Where(a => a.GroupId == groupId && memberIds.Contains(a.UserId) && a.CreatedAt >= weekStart && a.CreatedAt < weekEnd)
                .Select(a => new { ActionType = a.ActionType, TaskPriority = a.TaskPriority ?? 0, TaskSeverity = a.TaskSeverity ?? 0 })
                .ToListAsync();

            var activityScore = activityLogs.Sum(a =>
                ActivityScoreHelper.GetScore(a.ActionType, a.TaskPriority, a.TaskSeverity));

            // Count group messages from all members in the week
            var messageCount = await context.GroupMessages
                .AsNoTracking()
                .Where(m => m.GroupId == groupId && memberIds.Contains(m.UserId) && m.CreatedAt >= weekStart && m.CreatedAt < weekEnd)
                .CountAsync();

            var totalScore = activityScore + messageCount;

            return memberIds.Count > 0 ? totalScore / memberIds.Count : null;
        }
    }
}
