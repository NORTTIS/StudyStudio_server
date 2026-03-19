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

        // ==================== STUDIO ANALYTICS ====================

        public async Task<StudioAnalytics?> GetStudioAnalyticsByDateAsync(Guid studioId, DateOnly date)
        {
            return await _context.StudioAnalytics
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.StudioId == studioId && x.Date == date);
        }

        public async Task<List<StudioAnalytics>> GetStudioAnalyticsRangeAsync(Guid studioId, DateOnly startDate, DateOnly endDate)
        {
            return await _context.StudioAnalytics
                .AsNoTracking()
                .Where(x => x.StudioId == studioId && x.Date >= startDate && x.Date <= endDate)
                .OrderBy(x => x.Date)
                .ToListAsync();
        }

        public async Task<List<StudioAnalytics>> GetAllStudioAnalyticsRangeAsync(DateOnly startDate, DateOnly endDate)
        {
            return await _context.StudioAnalytics
                .AsNoTracking()
                .Where(x => x.Date >= startDate && x.Date <= endDate)
                .OrderBy(x => x.Date)
                .ToListAsync();
        }

        public async Task UpsertStudioAnalyticsAsync(StudioAnalytics analytics)
        {
            var existing = await _context.StudioAnalytics
                .FirstOrDefaultAsync(x => x.StudioId == analytics.StudioId && x.Date == analytics.Date);

            if (existing != null)
            {
                existing.TotalGroups = analytics.TotalGroups;
                existing.ActiveGroups = analytics.ActiveGroups;
                existing.TotalMembers = analytics.TotalMembers;
                existing.ActiveMembers = analytics.ActiveMembers;
                existing.TasksCompleted = analytics.TasksCompleted;
                existing.OverallCompletionRate = analytics.OverallCompletionRate;
                existing.EngagementScore = analytics.EngagementScore;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                analytics.CreatedAt = DateTime.UtcNow;
                analytics.UpdatedAt = DateTime.UtcNow;
                _context.StudioAnalytics.Add(analytics);
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
    }
}
