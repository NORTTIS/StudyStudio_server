using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository for analytics data operations
    /// </summary>
    public class AnalyticsRepository : IAnalyticsRepository
    {
        private readonly StudioDbContext _context;

        public AnalyticsRepository(StudioDbContext context)
        {
            _context = context;
        }

        // ==================== USER ACTIVITY METRICS ====================

        public async Task<UserActivityMetrics?> GetUserActivityByDateAsync(Guid userId, DateOnly date)
        {
            return await _context.UserActivityMetrics
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Date == date);
        }

        public async Task<List<UserActivityMetrics>> GetUserActivityRangeAsync(Guid userId, DateOnly startDate, DateOnly endDate)
        {
            return await _context.UserActivityMetrics
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.Date >= startDate && x.Date <= endDate)
                .OrderBy(x => x.Date)
                .ToListAsync();
        }

        public async Task UpsertUserActivityAsync(UserActivityMetrics metrics)
        {
            var existing = await _context.UserActivityMetrics
                .FirstOrDefaultAsync(x => x.UserId == metrics.UserId && x.Date == metrics.Date);

            if (existing != null)
            {
                existing.TasksCreated = metrics.TasksCreated;
                existing.TasksCompleted = metrics.TasksCompleted;
                existing.CommentsPosted = metrics.CommentsPosted;
                existing.MessagesSent = metrics.MessagesSent;
                existing.TotalActivityCount = metrics.TotalActivityCount;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                metrics.CreatedAt = DateTime.UtcNow;
                metrics.UpdatedAt = DateTime.UtcNow;
                _context.UserActivityMetrics.Add(metrics);
            }

            await _context.SaveChangesAsync();
        }

        // ==================== USER PRODUCTIVITY SCORES ====================

        public async Task<UserProductivityScores?> GetUserProductivityAsync(Guid userId, Guid? groupId, DateOnly weekStart)
        {
            return await _context.UserProductivityScores
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.GroupId == groupId && x.WeekStart == weekStart);
        }

        public async Task<List<UserProductivityScores>> GetUserProductivityRangeAsync(Guid userId, DateOnly startWeek, DateOnly endWeek)
        {
            return await _context.UserProductivityScores
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.WeekStart >= startWeek && x.WeekStart <= endWeek)
                .OrderBy(x => x.WeekStart)
                .ToListAsync();
        }

        public async Task UpsertUserProductivityAsync(UserProductivityScores score)
        {
            var existing = await _context.UserProductivityScores
                .FirstOrDefaultAsync(x => x.UserId == score.UserId && x.GroupId == score.GroupId && x.WeekStart == score.WeekStart);

            if (existing != null)
            {
                existing.ProductivityScore = score.ProductivityScore;
                existing.TasksCompleted = score.TasksCompleted;
                existing.TasksCreated = score.TasksCreated;
                existing.OnTimeCompletionRate = score.OnTimeCompletionRate;
                existing.AverageTaskCompletionHours = score.AverageTaskCompletionHours;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                score.CreatedAt = DateTime.UtcNow;
                score.UpdatedAt = DateTime.UtcNow;
                _context.UserProductivityScores.Add(score);
            }

            await _context.SaveChangesAsync();
        }

        // ==================== GROUP ANALYTICS ====================

        public async Task<GroupAnalytics?> GetGroupAnalyticsByDateAsync(Guid groupId, DateOnly date)
        {
            return await _context.GroupAnalytics
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.GroupId == groupId && x.Date == date);
        }

        public async Task<List<GroupAnalytics>> GetGroupAnalyticsRangeAsync(Guid groupId, DateOnly startDate, DateOnly endDate)
        {
            return await _context.GroupAnalytics
                .AsNoTracking()
                .Where(x => x.GroupId == groupId && x.Date >= startDate && x.Date <= endDate)
                .OrderBy(x => x.Date)
                .ToListAsync();
        }

        public async Task<List<GroupAnalytics>> GetGroupAnalyticsRangeAsync(StudioDbContext context, Guid groupId, DateOnly startDate, DateOnly endDate)
        {
            return await context.GroupAnalytics
                .AsNoTracking()
                .Where(x => x.GroupId == groupId && x.Date >= startDate && x.Date <= endDate)
                .OrderBy(x => x.Date)
                .ToListAsync();
        }

        public async Task<List<GroupAnalytics>> GetAllGroupAnalyticsRangeAsync(DateOnly startDate, DateOnly endDate)
        {
            return await _context.GroupAnalytics
                .AsNoTracking()
                .Where(x => x.Date >= startDate && x.Date <= endDate)
                .OrderBy(x => x.Date)
                .ToListAsync();
        }

        public async Task UpsertGroupAnalyticsAsync(GroupAnalytics analytics)
        {
            var existing = await _context.GroupAnalytics
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
                _context.GroupAnalytics.Add(analytics);
            }

            await _context.SaveChangesAsync();
        }

        // ==================== TASK PERFORMANCE METRICS ====================

        public async Task<TaskPerformanceMetrics?> GetTaskPerformanceAsync(Guid taskId)
        {
            return await _context.TaskPerformanceMetrics
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TaskId == taskId);
        }

        public async Task<List<TaskPerformanceMetrics>> GetTaskPerformanceRangeAsync(Guid? userId, Guid? groupId, DateOnly startDate, DateOnly endDate)
        {
            var query = _context.TaskPerformanceMetrics.AsNoTracking();

            if (userId.HasValue)
                query = query.Where(x => x.UserId == userId.Value);
            if (groupId.HasValue)
                query = query.Where(x => x.GroupId == groupId.Value);

            return await query
                .Where(x => x.CreatedAt >= DateTime.SpecifyKind(startDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc) && x.CreatedAt <= DateTime.SpecifyKind(endDate.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc))
                .ToListAsync();
        }

        public async Task UpsertTaskPerformanceAsync(TaskPerformanceMetrics metrics)
        {
            var existing = await _context.TaskPerformanceMetrics
                .FirstOrDefaultAsync(x => x.TaskId == metrics.TaskId);

            if (existing != null)
            {
                existing.EstimatedHours = metrics.EstimatedHours;
                existing.ActualHours = metrics.ActualHours;
                existing.HourVariance = metrics.HourVariance;
                existing.CompletedOnTime = metrics.CompletedOnTime;
                existing.DaysEarlyOrLate = metrics.DaysEarlyOrLate;
                existing.CompletedAt = metrics.CompletedAt;
                existing.DueDate = metrics.DueDate;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                metrics.CreatedAt = DateTime.UtcNow;
                metrics.UpdatedAt = DateTime.UtcNow;
                _context.TaskPerformanceMetrics.Add(metrics);
            }

            await _context.SaveChangesAsync();
        }

        // ==================== AGGREGATION HELPERS FOR ETL JOBS ====================

        public async Task<Dictionary<Guid, int>> AggregateTasksCreatedByUserAsync(DateTime from, DateTime to)
        {
            return await _context.Tasks
                .Where(t => t.CreatedAt >= from && t.CreatedAt <= to)
                .GroupBy(t => t.OwnerId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);
        }

        public async Task<Dictionary<Guid, int>> AggregateTasksCompletedByUserAsync(DateTime from, DateTime to)
        {
            return await _context.Tasks
                .Where(t => t.CompletedAt.HasValue && t.CompletedAt >= from && t.CompletedAt <= to)
                .GroupBy(t => t.OwnerId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);
        }

        public async Task<Dictionary<Guid, int>> AggregateCommentsByUserAsync(DateTime from, DateTime to)
        {
            return await _context.TaskComments
                .Where(c => c.CreatedAt >= from && c.CreatedAt <= to)
                .GroupBy(c => c.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);
        }

        public async Task<Dictionary<Guid, int>> AggregateMessagesByUserAsync(DateTime from, DateTime to)
        {
            return await _context.GroupMessages
                .Where(m => m.CreatedAt >= from && m.CreatedAt <= to)
                .GroupBy(m => m.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);
        }

        public async Task<Dictionary<Guid, int>> AggregateTasksByGroupAsync(Guid groupId, DateTime from, DateTime to)
        {
            return await _context.Tasks
                .Where(t => t.GroupId == groupId && t.CreatedAt >= from && t.CreatedAt <= to)
                .GroupBy(t => t.GroupId!.Value)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.GroupId, x => x.Count);
        }

        public async Task<Dictionary<Guid, int>> AggregateCompletedTasksByGroupAsync(Guid groupId, DateTime from, DateTime to)
        {
            return await _context.Tasks
                .Where(t => t.GroupId == groupId && t.CompletedAt.HasValue && t.CompletedAt >= from && t.CompletedAt <= to)
                .GroupBy(t => t.GroupId!.Value)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.GroupId, x => x.Count);
        }

        public async Task<Dictionary<Guid, int>> AggregateOverdueTasksByGroupAsync(Guid groupId, DateTime from, DateTime to)
        {
            var now = DateTime.UtcNow;
            return await _context.Tasks
                .Where(t => t.GroupId == groupId && t.DueDate < now && t.Progress < 100)
                .GroupBy(t => t.GroupId!.Value)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.GroupId, x => x.Count);
        }

        public async Task<Dictionary<Guid, int>> AggregateActiveMembersByGroupAsync(Guid groupId, DateTime from, DateTime to)
        {
            var activityUserIds = await _context.ActivityLogs
                .Where(a => a.GroupId == groupId && a.CreatedAt >= from && a.CreatedAt <= to)
                .Select(a => a.UserId)
                .Distinct()
                .ToListAsync();

            var participantUserIds = await _context.GroupParticipants
                .Where(p => p.GroupId == groupId)
                .Select(p => p.UserId)
                .ToListAsync();

            var activeMembers = activityUserIds.Intersect(participantUserIds).Count();
            return new Dictionary<Guid, int> { { groupId, activeMembers } };
        }

        public async Task<Dictionary<Guid, int>> AggregateMessagesByGroupAsync(Guid groupId, DateTime from, DateTime to)
        {
            return await _context.GroupMessages
                .Where(m => m.GroupId == groupId && m.CreatedAt >= from && m.CreatedAt <= to)
                .GroupBy(m => m.GroupId)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.GroupId, x => x.Count);
        }

        public async Task<Dictionary<Guid, int>> AggregateCommentsByGroupAsync(Guid groupId, DateTime from, DateTime to)
        {
            return await _context.TaskComments
                .Where(c => c.Task.GroupId == groupId && c.CreatedAt >= from && c.CreatedAt <= to)
                .GroupBy(c => c.Task.GroupId!.Value)
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
        public async Task<Dictionary<Guid, (int Done, int InProgress, int Todo, int Overdue, int Total)>> GetMemberTaskStatusBreakdownAsync(
            Guid groupId, DateTime from, DateTime to)
        {
            return await GetMemberTaskStatusBreakdownAsync(_context, groupId, from, to);
        }

        public async Task<Dictionary<Guid, (int Done, int InProgress, int Todo, int Overdue, int Total)>> GetMemberTaskStatusBreakdownAsync(
            StudioDbContext context, Guid groupId, DateTime from, DateTime to)
        {
            var tasks = await context.Tasks
                .AsNoTracking()
                .Where(t => t.GroupId == groupId && t.CreatedAt >= from && t.CreatedAt <= to)
                .Select(t => new
                {
                    t.OwnerId,
                    t.Progress,
                    t.CompletedAt,
                    t.DueDate,
                    IsDone = t.Progress == 100 || t.CompletedAt != null,
                    IsOverdue = t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow && t.Progress < 100,
                    IsInProgress = t.Progress > 0 && t.Progress < 100 && (!t.DueDate.HasValue || t.DueDate.Value >= DateTime.UtcNow)
                })
                .ToListAsync();

            return tasks
                .GroupBy(t => t.OwnerId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var done = g.Count(t => t.IsDone);
                        var overdue = g.Count(t => t.IsOverdue);
                        var inProgress = g.Count(t => t.IsInProgress && !t.IsDone && !t.IsOverdue);
                        var todo = g.Count(t => !t.IsDone && !t.IsInProgress && !t.IsOverdue);
                        var total = g.Count();
                        return (done, inProgress, todo, overdue, total);
                    });
        }

        /// <summary>
        /// Get daily completed tasks count per member within date range
        /// </summary>
        public async Task<Dictionary<Guid, Dictionary<DateOnly, int>>> GetMemberDailyCompletionsAsync(
            Guid groupId, DateOnly startDate, DateOnly endDate)
        {
            return await GetMemberDailyCompletionsAsync(_context, groupId, startDate, endDate);
        }

        public async Task<Dictionary<Guid, Dictionary<DateOnly, int>>> GetMemberDailyCompletionsAsync(
            StudioDbContext context, Guid groupId, DateOnly startDate, DateOnly endDate)
        {
            var startDateTime = DateTime.SpecifyKind(startDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var endDateTime = DateTime.SpecifyKind(endDate.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

            var completedTasks = await context.Tasks
                .AsNoTracking()
                .Where(t => t.GroupId == groupId
                            && t.CompletedAt.HasValue
                            && t.CompletedAt >= startDateTime
                            && t.CompletedAt <= endDateTime)
                .Select(t => new
                {
                    t.OwnerId,
                    CompletedDate = DateOnly.FromDateTime(t.CompletedAt!.Value)
                })
                .ToListAsync();

            var result = completedTasks
                .GroupBy(t => t.OwnerId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .GroupBy(t => t.CompletedDate)
                        .ToDictionary(g2 => g2.Key, g2 => g2.Count()));

            // Ensure all members have entries for all dates (fill with 0)
            var ownerIds = completedTasks.Select(t => t.OwnerId).Distinct().ToList();
            foreach (var ownerId in ownerIds)
            {
                if (!result.ContainsKey(ownerId))
                    result[ownerId] = new Dictionary<DateOnly, int>();

                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    if (!result[ownerId].ContainsKey(date))
                        result[ownerId][date] = 0;
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
            return await GetMemberLastActivityAsync(_context, groupId);
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
        /// </summary>
        public async Task<Dictionary<Guid, (int Done, int InProgress, int Todo, int Overdue, int Total)>> GetMemberTaskStatusBreakdownAllTimeAsync(Guid groupId)
        {
            var tasks = await _context.Tasks
                .AsNoTracking()
                .Where(t => t.GroupId == groupId)
                .Select(t => new
                {
                    t.OwnerId,
                    t.Progress,
                    t.CompletedAt,
                    t.DueDate,
                    IsDone = t.Progress == 100 || t.CompletedAt != null,
                    IsOverdue = t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow && t.Progress < 100,
                    IsInProgress = t.Progress > 0 && t.Progress < 100 && (!t.DueDate.HasValue || t.DueDate.Value >= DateTime.UtcNow)
                })
                .ToListAsync();

            return tasks
                .GroupBy(t => t.OwnerId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var done = g.Count(t => t.IsDone);
                        var overdue = g.Count(t => t.IsOverdue);
                        var inProgress = g.Count(t => t.IsInProgress && !t.IsDone && !t.IsOverdue);
                        var todo = g.Count(t => !t.IsDone && !t.IsInProgress && !t.IsOverdue);
                        var total = g.Count();
                        return (done, inProgress, todo, overdue, total);
                    });
        }
    }
}
