using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.Globalization;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service handling analytics and dashboard data
    /// </summary>
    public class AnalyticsService : IAnalyticsService
    {
        private readonly StudioDbContext _context;
        private readonly IAnalyticsRepository _analyticsRepository;

        public AnalyticsService(StudioDbContext context, IAnalyticsRepository analyticsRepository)
        {
            _context = context;
            _analyticsRepository = analyticsRepository;
        }

        // ==================== USER DASHBOARD ====================

        /// <summary>
        /// Get user dashboard with productivity score, activity heatmap, task completion trend, and deadline performance
        /// </summary>
        public async Task<UserDashboardResponse> GetUserDashboardAsync(Guid userId, DateOnly? startDate, DateOnly? endDate)
        {
            var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var start = startDate ?? end.AddDays(-30);

            var productivityScore = await CalculateProductivityScoreAsync(userId, start, end);
            var activityHeatmap = await GetUserActivityHeatmapAsync(userId, 30);
            var taskCompletionTrend = await GetTaskCompletionTrendAsync(userId, 30);
            var deadlinePerformance = await GetDeadlinePerformanceAsync(userId);

            return new UserDashboardResponse
            {
                ProductivityScore = productivityScore,
                ActivityHeatmap = activityHeatmap,
                TaskCompletionTrend = taskCompletionTrend,
                DeadlinePerformance = deadlinePerformance
            };
        }

        /// <summary>
        /// Get user activity heatmap data
        /// </summary>
        public async Task<List<ActivityHeatmapData>> GetUserActivityHeatmapAsync(Guid userId, int days = 30)
        {
            var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));
            var endDate = DateOnly.FromDateTime(DateTime.UtcNow);

            var metrics = await _analyticsRepository.GetUserActivityRangeAsync(userId, startDate, endDate);

            // Generate all dates in range
            var result = new List<ActivityHeatmapData>();
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var metric = metrics.FirstOrDefault(m => m.Date == date);
                result.Add(new ActivityHeatmapData
                {
                    Date = date,
                    ActivityCount = metric?.TotalActivityCount ?? 0
                });
            }

            return result;
        }

        /// <summary>
        /// Get task completion trend over time
        /// </summary>
        public async Task<List<TaskCompletionTrendData>> GetTaskCompletionTrendAsync(Guid userId, int days = 30)
        {
            var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));
            var endDate = DateOnly.FromDateTime(DateTime.UtcNow);

            // Get raw task data for the period
            var startDateTime = DateTime.SpecifyKind(startDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var endDateTime = DateTime.SpecifyKind(endDate.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

            var tasks = await _context.Tasks
                .Where(t => t.OwnerId == userId &&
                           (t.CreatedAt >= startDateTime && t.CreatedAt <= endDateTime ||
                            t.CompletedAt.HasValue && t.CompletedAt >= startDateTime && t.CompletedAt <= endDateTime))
                .ToListAsync();

            var result = new List<TaskCompletionTrendData>();
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var dayStart = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
                var dayEnd = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

                var created = tasks.Count(t => t.CreatedAt >= dayStart && t.CreatedAt <= dayEnd);
                var completed = tasks.Count(t => t.CompletedAt.HasValue &&
                                                 t.CompletedAt.Value >= dayStart &&
                                                 t.CompletedAt.Value <= dayEnd);

                result.Add(new TaskCompletionTrendData
                {
                    Date = date,
                    TasksCreated = created,
                    TasksCompleted = completed
                });
            }

            return result;
        }

        /// <summary>
        /// Get deadline performance (on-time vs late completion)
        /// </summary>
        public async Task<DeadlinePerformanceData> GetDeadlinePerformanceAsync(Guid userId)
        {
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

            var completedTasks = await _context.Tasks
                .Where(t => t.OwnerId == userId &&
                           t.CompletedAt.HasValue &&
                           t.CompletedAt >= thirtyDaysAgo &&
                           t.DueDate.HasValue)
                .ToListAsync();

            var onTime = completedTasks.Count(t => t.CompletedAt <= t.DueDate);
            var late = completedTasks.Count(t => t.CompletedAt > t.DueDate);
            var total = onTime + late;

            return new DeadlinePerformanceData
            {
                OnTimeCount = onTime,
                LateCount = late,
                OnTimePercentage = total > 0 ? Math.Round((double)onTime / total * 100, 2) : 0
            };
        }

        private async Task<double> CalculateProductivityScoreAsync(Guid userId, DateOnly startDate, DateOnly endDate)
        {
            var metrics = await _analyticsRepository.GetUserActivityRangeAsync(userId, startDate, endDate);

            if (!metrics.Any())
                return 0;

            // Calculate weighted productivity score
            var totalTasksCompleted = metrics.Sum(m => m.TasksCompleted);
            var totalTasksCreated = metrics.Sum(m => m.TasksCreated);
            var totalComments = metrics.Sum(m => m.CommentsPosted);
            var totalMessages = metrics.Sum(m => m.MessagesSent);
            var totalActivity = metrics.Sum(m => m.TotalActivityCount);

            // Weighted scoring: Tasks completed (40%), Tasks created (20%), Comments (20%), Messages (20%)
            var taskScore = Math.Min(totalTasksCompleted * 5, 40);
            var creationScore = Math.Min(totalTasksCreated * 2, 20);
            var commentScore = Math.Min(totalComments * 2, 20);
            var messageScore = Math.Min(totalMessages * 1, 20);

            return Math.Round((double)taskScore + creationScore + commentScore + messageScore, 2);
        }

        // ==================== GROUP ANALYTICS ====================

        /// <summary>
        /// Get group analytics dashboard
        /// </summary>
        public async Task<GroupAnalyticsResponse> GetGroupAnalyticsAsync(Guid groupId, Guid userId, DateOnly? startDate, DateOnly? endDate)
        {
            var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var start = startDate ?? end.AddDays(-30);

            // Check if user is member of the group
            var isMember = await _context.GroupParticipants
                .AnyAsync(p => p.GroupId == groupId && p.UserId == userId);

            if (!isMember)
                throw new AppException(Exceptions.ErrorCodes.GroupPermissionDenied, StatusCodes.Status403Forbidden);

            var progress = await GetGroupProgressAsync(groupId, start, end);
            var performanceRadar = await GetGroupPerformanceRadarAsync(groupId);
            var memberContribution = await GetGroupMemberContributionAsync(groupId);
            var activityHeatmap = await GetGroupActivityHeatmapAsync(groupId, 30);

            // Calculate completion rate
            var latestProgress = progress.LastOrDefault();
            var completionRate = latestProgress?.CompletionRate ?? 0;

            return new GroupAnalyticsResponse
            {
                CompletionRate = completionRate,
                Progress = progress,
                PerformanceRadar = performanceRadar,
                MemberContribution = memberContribution,
                ActivityHeatmap = activityHeatmap
            };
        }

        /// <summary>
        /// Get group member contributions
        /// </summary>
        public async Task<List<MemberContributionData>> GetGroupMemberContributionAsync(Guid groupId)
        {
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

            // Get all members with user info
            var memberUserIds = await _context.GroupParticipants
                .Where(p => p.GroupId == groupId)
                .Select(p => p.UserId)
                .ToListAsync();

            var users = await _context.Users
                .Where(u => memberUserIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.FirstName, u.LastName })
                .ToDictionaryAsync(u => u.UserId, u => $"{u.FirstName} {u.LastName}");

            // Get tasks completed by each member
            var tasksCompleted = await _context.Tasks
                .Where(t => t.GroupId == groupId && t.CompletedAt >= thirtyDaysAgo)
                .GroupBy(t => t.OwnerId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            // Get tasks created by each member
            var tasksCreated = await _context.Tasks
                .Where(t => t.GroupId == groupId && t.CreatedAt >= thirtyDaysAgo)
                .GroupBy(t => t.OwnerId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            // Get messages sent by each member
            var messagesSent = await _context.GroupMessages
                .Where(m => m.GroupId == groupId && m.CreatedAt >= thirtyDaysAgo)
                .GroupBy(m => m.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            var result = memberUserIds.Select(userId =>
            {
                var completed = tasksCompleted.GetValueOrDefault(userId, 0);
                var created = tasksCreated.GetValueOrDefault(userId, 0);
                var messages = messagesSent.GetValueOrDefault(userId, 0);
                var total = completed + created + messages;

                return new MemberContributionData
                {
                    UserId = userId,
                    UserName = users.GetValueOrDefault(userId, "Unknown"),
                    TasksCompleted = completed,
                    TasksCreated = created,
                    MessagesSent = messages,
                    ContributionPercentage = total > 0 ? Math.Round((double)total / (completed + created + messages) * 100, 2) : 0
                };
            }).ToList();

            // Calculate percentages
            var totalContribution = result.Sum(r => r.TasksCompleted + r.TasksCreated + r.MessagesSent);
            if (totalContribution > 0)
            {
                foreach (var r in result)
                {
                    r.ContributionPercentage = Math.Round(
                        (double)(r.TasksCompleted + r.TasksCreated + r.MessagesSent) / totalContribution * 100, 2);
                }
            }

            return result.OrderByDescending(r => r.ContributionPercentage).ToList();
        }

        private async Task<List<GroupProgressData>> GetGroupProgressAsync(Guid groupId, DateOnly startDate, DateOnly endDate)
        {
            var analytics = await _analyticsRepository.GetGroupAnalyticsRangeAsync(groupId, startDate, endDate);

            var result = new List<GroupProgressData>();
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var metric = analytics.FirstOrDefault(a => a.Date == date);
                result.Add(new GroupProgressData
                {
                    Date = date,
                    TotalTasks = metric?.TotalTasks ?? 0,
                    CompletedTasks = metric?.CompletedTasks ?? 0,
                    CompletionRate = metric?.CompletionRate ?? 0
                });
            }

            return result;
        }

        private async Task<List<PerformanceRadarData>> GetGroupPerformanceRadarAsync(Guid groupId)
        {
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

            // Get group analytics
            var analytics = await _analyticsRepository.GetGroupAnalyticsRangeAsync(
                groupId,
                DateOnly.FromDateTime(thirtyDaysAgo),
                DateOnly.FromDateTime(DateTime.UtcNow));

            var latest = analytics.LastOrDefault();

            return new List<PerformanceRadarData>
            {
                new() { Metric = "Task Completion", Score = latest?.CompletionRate ?? 0 },
                new() { Metric = "Member Activity", Score = latest?.ActiveMembers > 0 ? 100 : 0 },
                new() { Metric = "Communication", Score = Math.Min((latest?.MessagesCount ?? 0) * 10, 100) },
                new() { Metric = "Collaboration", Score = Math.Min((latest?.CommentsCount ?? 0) * 10, 100) },
                new() { Metric = "Overdue Control", Score = latest?.OverdueTasks == 0 ? 100 : Math.Max(100 - (latest?.OverdueTasks ?? 0) * 20, 0) }
            };
        }

        private async Task<List<GroupActivityHeatmapData>> GetGroupActivityHeatmapAsync(Guid groupId, int days = 30)
        {
            var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));
            var endDate = DateOnly.FromDateTime(DateTime.UtcNow);

            var analytics = await _analyticsRepository.GetGroupAnalyticsRangeAsync(groupId, startDate, endDate);

            var result = new List<GroupActivityHeatmapData>();
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var metric = analytics.FirstOrDefault(a => a.Date == date);
                result.Add(new GroupActivityHeatmapData
                {
                    Date = date,
                    ActivityCount = (metric?.MessagesCount ?? 0) + (metric?.CommentsCount ?? 0)
                });
            }

            return result;
        }

        // ==================== STUDIO ANALYTICS ====================

        /// <summary>
        /// Get studio analytics dashboard
        /// </summary>
        public async Task<StudioAnalyticsResponse> GetStudioAnalyticsAsync(Guid studioId, Guid userId, DateOnly? startDate, DateOnly? endDate)
        {
            var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var start = startDate ?? end.AddDays(-30);

            // Check if user is member of the studio
            var isMember = await _context.StudioParticipants
                .AnyAsync(p => p.StudioId == studioId && p.UserId == userId);

            if (!isMember)
                throw new AppException(Exceptions.ErrorCodes.StudioPermissionDenied, StatusCodes.Status403Forbidden);

            var completionRateHistory = await GetStudioProgressAsync(studioId, start, end);
            var groupComparison = await GetStudioGroupComparisonAsync(studioId);
            var groupHeatmapComparison = await GetGroupHeatmapComparisonAsync(studioId, 30);

            var latestAnalytics = completionRateHistory.LastOrDefault();

            return new StudioAnalyticsResponse
            {
                CompletionRate = latestAnalytics?.CompletionRate ?? 0,
                ActiveUsers = latestAnalytics?.ActiveUsers ?? 0,
                EngagementScore = Math.Round((latestAnalytics?.CompletionRate ?? 0) * 0.4 + (latestAnalytics?.ActiveUsers > 0 ? 60 : 0), 2),
                GroupComparison = groupComparison,
                CompletionRateHistory = completionRateHistory,
                GroupHeatmapComparison = groupHeatmapComparison
            };
        }

        /// <summary>
        /// Get studio group comparison
        /// </summary>
        public async Task<List<GroupComparisonData>> GetStudioGroupComparisonAsync(Guid studioId)
        {
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

            var groups = await _context.Groups
                .Where(g => g.StudioId == studioId)
                .Select(g => g.GroupId)
                .ToListAsync();

            var result = new List<GroupComparisonData>();

            foreach (var groupId in groups)
            {
                var totalTasks = await _context.Tasks
                    .Where(t => t.GroupId == groupId)
                    .CountAsync();

                var completedTasks = await _context.Tasks
                    .Where(t => t.GroupId == groupId && t.Progress == 100)
                    .CountAsync();

                var activeMembers = await _context.ActivityLogs
                    .Where(a => a.GroupId == groupId && a.CreatedAt >= thirtyDaysAgo)
                    .Select(a => a.UserId)
                    .Distinct()
                    .CountAsync();

                var group = await _context.Groups.FindAsync(groupId);

                result.Add(new GroupComparisonData
                {
                    GroupId = groupId,
                    GroupName = group?.GroupName ?? "Unknown",
                    TotalTasks = totalTasks,
                    CompletedTasks = completedTasks,
                    CompletionRate = totalTasks > 0 ? Math.Round((double)completedTasks / totalTasks * 100, 2) : 0,
                    ActiveMembers = activeMembers
                });
            }

            return result.OrderByDescending(g => g.CompletionRate).ToList();
        }

        private async Task<List<StudioProgressData>> GetStudioProgressAsync(Guid studioId, DateOnly startDate, DateOnly endDate)
        {
            var analytics = await _analyticsRepository.GetStudioAnalyticsRangeAsync(studioId, startDate, endDate);

            var result = new List<StudioProgressData>();
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var metric = analytics.FirstOrDefault(a => a.Date == date);
                result.Add(new StudioProgressData
                {
                    Date = date,
                    CompletionRate = metric?.OverallCompletionRate ?? 0,
                    ActiveUsers = metric?.ActiveMembers ?? 0
                });
            }

            return result;
        }

        /// <summary>
        /// Get heatmap comparison across groups in a studio
        /// Compare activity heatmap between groups
        /// </summary>
        public async Task<List<GroupHeatmapComparisonData>> GetGroupHeatmapComparisonAsync(Guid studioId, int days = 30)
        {
            var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
            var startDate = endDate.AddDays(-days);
            var startDateTime = DateTime.SpecifyKind(startDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var endDateTime = DateTime.SpecifyKind(endDate.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

            // Get all groups in studio
            var groups = await _context.Groups
                .Where(g => g.StudioId == studioId)
                .Select(g => new { g.GroupId, g.GroupName })
                .ToListAsync();

            var result = new List<GroupHeatmapComparisonData>();

            // For each date, get activity for all groups
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var dayStart = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
                var dayEnd = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

                var groupItems = new List<GroupActivityItem>();

                foreach (var group in groups)
                {
                    // Get messages count
                    var messagesCount = await _context.GroupMessages
                        .Where(m => m.GroupId == group.GroupId && m.CreatedAt >= dayStart && m.CreatedAt <= dayEnd)
                        .CountAsync();

                    // Get comments count
                    var commentsCount = await _context.TaskComments
                        .Where(c => c.Task.GroupId == group.GroupId && c.CreatedAt >= dayStart && c.CreatedAt <= dayEnd)
                        .CountAsync();

                    // Get tasks completed
                    var tasksCompleted = await _context.Tasks
                        .Where(t => t.GroupId == group.GroupId && t.CompletedAt >= dayStart && t.CompletedAt <= dayEnd)
                        .CountAsync();

                    groupItems.Add(new GroupActivityItem
                    {
                        GroupId = group.GroupId,
                        GroupName = group.GroupName,
                        ActivityCount = messagesCount + commentsCount + tasksCompleted,
                        MessagesCount = messagesCount,
                        CommentsCount = commentsCount,
                        TasksCompleted = tasksCompleted
                    });
                }

                result.Add(new GroupHeatmapComparisonData
                {
                    Date = date,
                    Groups = groupItems
                });
            }

            return result;
        }
    }
}
