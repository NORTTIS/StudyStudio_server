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
        /// Get group analytics dashboard — now includes all data for GroupAnalyticPage
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

            // Run all data fetches sequentially (avoids DbContext concurrency issues)
            var progress = await GetGroupProgressAsync(groupId, start, end);
            var performanceRadar = await GetGroupPerformanceRadarAsync(groupId);
            var memberContribution = await GetGroupMemberContributionAsync(groupId);
            var activityHeatmap = await GetGroupActivityHeatmapAsync(groupId, 30);
            var memberTaskBreakdown = await GetMemberTaskBreakdownAsync(groupId, start, end);
            var memberProgressTrend = await GetMemberProgressTrendAsync(groupId, start, end);
            var memberHeatmap = await GetMemberHeatmapAsync(groupId, start, end);
            var memberActivitySummary = await GetMemberActivitySummaryAsync(groupId, start, end);

            // Calculate completion rate
            var latestProgress = progress.LastOrDefault();
            var completionRate = latestProgress?.CompletionRate ?? 0;

            return new GroupAnalyticsResponse
            {
                CompletionRate = completionRate,
                Progress = progress,
                PerformanceRadar = performanceRadar,
                MemberContribution = memberContribution,
                ActivityHeatmap = activityHeatmap,
                // New fields for GroupAnalyticPage
                MemberTaskBreakdown = memberTaskBreakdown,
                MemberProgressTrend = memberProgressTrend,
                MemberHeatmap = memberHeatmap,
                MemberActivitySummary = memberActivitySummary
            };
        }

        /// <summary>
        /// Get group summary without date filter (all time) - for Chart 1, 2, 4, 6
        /// </summary>
        public async Task<GroupSummaryResponse> GetGroupSummaryAsync(Guid groupId, Guid userId)
        {
            // Check if user is member of the group
            var isMember = await _context.GroupParticipants
                .AnyAsync(p => p.GroupId == groupId && p.UserId == userId);

            if (!isMember)
                throw new AppException(Exceptions.ErrorCodes.GroupPermissionDenied, StatusCodes.Status403Forbidden);

            // Query all time data (no date filter)
            var memberTaskBreakdown = await GetMemberTaskBreakdownAllTimeAsync(groupId);
            var memberActivitySummary = await GetMemberActivitySummaryAllTimeAsync(groupId);
            var memberContribution = await GetGroupMemberContributionAsync(groupId);

            return new GroupSummaryResponse
            {
                MemberTaskBreakdown = memberTaskBreakdown,
                MemberActivitySummary = memberActivitySummary,
                MemberContribution = memberContribution
            };
        }

        private async Task<List<MemberTaskBreakdownData>> GetMemberTaskBreakdownAllTimeAsync(Guid groupId)
        {
            var breakdown = await _analyticsRepository.GetMemberTaskStatusBreakdownAllTimeAsync(groupId);

            var memberUserIds = await _context.GroupParticipants
                .Where(p => p.GroupId == groupId)
                .Select(p => p.UserId)
                .ToListAsync();

            var users = await _context.Users
                .Where(u => memberUserIds.Contains(u.UserId))
                .Select(u => new { u.UserId, FullName = u.FirstName + " " + u.LastName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);

            // Get messages sent per member
            var messagesSent = await _context.GroupMessages
                .Where(m => m.GroupId == groupId)
                .GroupBy(m => m.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            var totalDone = breakdown.Values.Sum(b => b.Done);
            var totalInProgress = breakdown.Values.Sum(b => b.InProgress);
            var totalTodo = breakdown.Values.Sum(b => b.Todo);
            var totalOverdue = breakdown.Values.Sum(b => b.Overdue);
            var totalActivity = totalDone + totalInProgress + totalTodo + totalOverdue;

            var result = memberUserIds.Select(userId =>
            {
                var (done, inProgress, todo, overdue, total) = breakdown.GetValueOrDefault(userId, (0, 0, 0, 0, 0));
                var contribution = totalActivity > 0
                    ? Math.Round((double)(done + inProgress + todo + overdue) / totalActivity * 100, 2)
                    : 0;

                return new MemberTaskBreakdownData
                {
                    UserId = userId,
                    UserName = users.GetValueOrDefault(userId, "Unknown"),
                    TotalTasks = total,
                    DoneTasks = done,
                    InProgressTasks = inProgress,
                    TodoTasks = todo,
                    OverdueTasks = overdue,
                    ContributionPercentage = contribution,
                    MessagesSent = messagesSent.GetValueOrDefault(userId, 0)
                };
            }).ToList();

            return result.OrderByDescending(r => r.DoneTasks).ToList();
        }

        private async Task<List<MemberActivitySummary>> GetMemberActivitySummaryAllTimeAsync(Guid groupId)
        {
            var taskBreakdown = await GetMemberTaskBreakdownAllTimeAsync(groupId);
            var lastActivity = await _analyticsRepository.GetMemberLastActivityAsync(groupId);

            var totalDone = taskBreakdown.Sum(b => b.DoneTasks);
            var totalActivity = taskBreakdown.Sum(b => b.TotalTasks);

            return taskBreakdown.Select(tb =>
            {
                var contribution = totalActivity > 0
                    ? Math.Round((double)tb.TotalTasks / totalActivity * 100, 2)
                    : 0;

                return new MemberActivitySummary
                {
                    UserId = tb.UserId,
                    UserName = tb.UserName,
                    TotalTasks = tb.TotalTasks,
                    CompletedTasks = tb.DoneTasks,
                    InProgressTasks = tb.InProgressTasks,
                    TodoTasks = tb.TodoTasks,
                    OverdueTasks = tb.OverdueTasks,
                    LastActivityAt = lastActivity.GetValueOrDefault(tb.UserId),
                    ContributionPercentage = contribution,
                    MessagesSent = tb.MessagesSent
                };
            }).ToList();
        }

        /// <summary>
        /// Get group member contributions with weighted scoring based on priority/severity
        /// Formula: Score = BasePoints × PriorityWeight × SeverityWeight
        /// Priority: Low=1.0, Medium=1.5, High=2.0
        /// Severity: Minor=1.0, Moderate=1.2, Major=1.5, Critical=2.0
        /// Base Points: Complete=10, Create=5, Update=3, Delete=2, Assign=1, Comment=1, Message=1
        /// </summary>
        public async Task<List<MemberContributionData>> GetGroupMemberContributionAsync(Guid groupId)
        {
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

            // Weight coefficients
            var priorityWeight = new[] { 1.0, 1.5, 2.0 };   // Low, Medium, High
            var severityWeight = new[] { 1.0, 1.2, 1.5, 2.0 };  // Minor, Moderate, Major, Critical

            // Base points per action
            const double CompletePoints = 10;
            const double CreatePoints = 5;
            const double UpdatePoints = 3;
            const double DeletePoints = 2;
            const double AssignPoints = 1;

            // Get all members with user info
            var memberUserIds = await _context.GroupParticipants
                .Where(p => p.GroupId == groupId)
                .Select(p => p.UserId)
                .ToListAsync();

            var users = await _context.Users
                .Where(u => memberUserIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.FirstName, u.LastName })
                .ToDictionaryAsync(u => u.UserId, u => $"{u.FirstName} {u.LastName}");

            // Get ActivityLogs with priority/severity (30 days)
            var activityLogs = await _context.ActivityLogs
                .Where(l => l.GroupId == groupId && l.CreatedAt >= thirtyDaysAgo)
                .ToListAsync();

            // Get messages sent directly from GroupMessages (not from ActivityLogs for consistency)
            var messagesByUser = await _context.GroupMessages
                .Where(m => m.GroupId == groupId && m.CreatedAt >= thirtyDaysAgo)
                .GroupBy(m => m.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            // Initialize member contribution data
            var memberScores = memberUserIds.ToDictionary(
                id => id,
                userId => new MemberContributionData { UserId = userId }
            );

            // Process activity logs
            foreach (var log in activityLogs)
            {
                if (!memberScores.ContainsKey(log.UserId)) continue;

                var priority = log.TaskPriority ?? 0;
                var severity = log.TaskSeverity ?? 0;
                var priorityW = priorityWeight[Math.Min(priority, 2)];
                var severityW = severityWeight[Math.Min(severity, 3)];

                switch (log.ActionType)
                {
                    case "TASK_CREATE":
                        memberScores[log.UserId].TasksCreated++;
                        memberScores[log.UserId].CreatedScore += CreatePoints * priorityW * severityW;
                        break;
                    case "TASK_COMPLETE":
                        memberScores[log.UserId].TasksCompleted++;
                        memberScores[log.UserId].CompletedScore += CompletePoints * priorityW * severityW;
                        break;
                    case "TASK_UPDATE":
                        memberScores[log.UserId].TasksUpdated++;
                        memberScores[log.UserId].UpdatedScore += UpdatePoints * priorityW * severityW;
                        break;
                    case "TASK_DELETE":
                        memberScores[log.UserId].TasksDeleted++;
                        memberScores[log.UserId].DeletedScore += DeletePoints * priorityW * severityW;
                        break;
                    case "TASK_ASSIGN":
                        memberScores[log.UserId].TasksAssigned++;
                        memberScores[log.UserId].AssignedScore += AssignPoints * priorityW * severityW;
                        break;
                    case "COMMENT_CREATE":
                        memberScores[log.UserId].CommentsCreated++;
                        break;
                    // Note: Messages are counted directly from GroupMessages table for consistency
                    // (not from ActivityLogs to match memberTaskBreakdown.messagesSent)
                }
            }

            // Calculate total scores and percentages
            foreach (var member in memberScores.Values)
            {
                member.UserName = users.GetValueOrDefault(member.UserId, "Unknown");
                // Add messages from GroupMessages table (consistent with memberTaskBreakdown)
                member.MessagesSent = messagesByUser.GetValueOrDefault(member.UserId, 0);
                member.TotalScore = member.CompletedScore + member.CreatedScore +
                                   member.UpdatedScore + member.DeletedScore +
                                   member.AssignedScore + member.CommentsCreated +
                                   member.MessagesSent;
            }

            var totalGroupScore = memberScores.Values.Sum(m => m.TotalScore);
            if (totalGroupScore > 0)
            {
                foreach (var member in memberScores.Values)
                {
                    member.ContributionPercentage = Math.Round(member.TotalScore / totalGroupScore * 100, 2);
                }
            }

            return memberScores.Values
                .OrderByDescending(m => m.TotalScore)
                .ToList();
        }

        // ==================== GROUP ANALYTICS ENHANCED: for GroupAnalyticPage ====================

        /// <summary>
        /// Get task status breakdown per member in a group (done, in-progress, todo, overdue)
        /// Powers Chart 1 (Personal Donut), Chart 2 (Group Donut), Chart 4 (Bar Chart)
        /// </summary>
        public async Task<List<MemberTaskBreakdownData>> GetMemberTaskBreakdownAsync(
            Guid groupId, DateOnly? startDate = null, DateOnly? endDate = null)
        {
            var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var start = startDate ?? end.AddDays(-30);
            var from = DateTime.SpecifyKind(start.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var to = DateTime.SpecifyKind(end.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

            // Get all group members with names
            var memberUserIds = await _context.GroupParticipants
                .Where(p => p.GroupId == groupId)
                .Select(p => p.UserId)
                .ToListAsync();

            var users = await _context.Users
                .Where(u => memberUserIds.Contains(u.UserId))
                .Select(u => new { u.UserId, FullName = u.FirstName + " " + u.LastName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);

            // Get task breakdown per member
            var breakdown = await _analyticsRepository.GetMemberTaskStatusBreakdownAsync(groupId, from, to);

            // Get messages sent per member
            var messagesSent = await _context.GroupMessages
                .Where(m => m.GroupId == groupId && m.CreatedAt >= from && m.CreatedAt <= to)
                .GroupBy(m => m.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            // Get contribution percentages
            var totalDone = breakdown.Values.Sum(b => b.Done);
            var totalInProgress = breakdown.Values.Sum(b => b.InProgress);
            var totalTodo = breakdown.Values.Sum(b => b.Todo);
            var totalOverdue = breakdown.Values.Sum(b => b.Overdue);
            var totalActivity = totalDone + totalInProgress + totalTodo + totalOverdue;

            var result = memberUserIds.Select(userId =>
            {
                var (done, inProgress, todo, overdue, total) = breakdown.GetValueOrDefault(userId, (0, 0, 0, 0, 0));
                var contribution = totalActivity > 0
                    ? Math.Round((double)(done + inProgress + todo + overdue) / totalActivity * 100, 2)
                    : 0;

                return new MemberTaskBreakdownData
                {
                    UserId = userId,
                    UserName = users.GetValueOrDefault(userId, "Unknown"),
                    TotalTasks = total,
                    DoneTasks = done,
                    InProgressTasks = inProgress,
                    TodoTasks = todo,
                    OverdueTasks = overdue,
                    ContributionPercentage = contribution,
                    MessagesSent = messagesSent.GetValueOrDefault(userId, 0)
                };
            }).ToList();

            return result.OrderByDescending(r => r.DoneTasks).ToList();
        }

        /// <summary>
        /// Get per-member daily completion trend
        /// Powers Chart 3 (Line Chart)
        /// </summary>
        public async Task<List<MemberProgressTrendData>> GetMemberProgressTrendAsync(
            Guid groupId, DateOnly? startDate = null, DateOnly? endDate = null)
        {
            var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var start = startDate ?? end.AddDays(-30);

            // Get all group members with names
            var memberUserIds = await _context.GroupParticipants
                .Where(p => p.GroupId == groupId)
                .Select(p => p.UserId)
                .ToListAsync();

            var users = await _context.Users
                .Where(u => memberUserIds.Contains(u.UserId))
                .Select(u => new { u.UserId, FullName = u.FirstName + " " + u.LastName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);

            // Get daily completions per member
            var dailyCompletions = await _analyticsRepository.GetMemberDailyCompletionsAsync(groupId, start, end);

            return memberUserIds.Select(userId =>
            {
                var memberDaily = dailyCompletions.GetValueOrDefault(userId, new Dictionary<DateOnly, int>());

                var dailyPoints = new List<DailyProgressPoint>();
                for (var date = start; date <= end; date = date.AddDays(1))
                {
                    dailyPoints.Add(new DailyProgressPoint
                    {
                        Date = date,
                        CompletedTasks = memberDaily.GetValueOrDefault(date, 0)
                    });
                }

                return new MemberProgressTrendData
                {
                    UserId = userId,
                    UserName = users.GetValueOrDefault(userId, "Unknown"),
                    DailyCompletions = dailyPoints
                };
            }).ToList();
        }

        /// <summary>
        /// Get per-member heatmap activity (activity level 0-4 per day)
        /// Powers Chart 5 (Member Heatmap)
        /// Uses weighted scoring: Task points = 10 × PriorityWeight × SeverityWeight
        /// </summary>
        public async Task<List<MemberHeatmapData>> GetMemberHeatmapAsync(
            Guid groupId, DateOnly? startDate = null, DateOnly? endDate = null)
        {
            var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var start = startDate ?? end.AddDays(-30);
            var startDateTime = DateTime.SpecifyKind(start.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var endDateTime = DateTime.SpecifyKind(end.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

            // Weight coefficients (same as contribution formula)
            var priorityWeight = new[] { 1.0, 1.5, 2.0 };   // Low, Medium, High
            var severityWeight = new[] { 1.0, 1.2, 1.5, 2.0 };  // Minor, Moderate, Major, Critical
            const double CompletePoints = 10;

            // Get all group members with names
            var memberUserIds = await _context.GroupParticipants
                .Where(p => p.GroupId == groupId)
                .Select(p => p.UserId)
                .ToListAsync();

            var users = await _context.Users
                .Where(u => memberUserIds.Contains(u.UserId))
                .Select(u => new { u.UserId, FullName = u.FirstName + " " + u.LastName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);

            // Get tasks completed per member per day WITH priority/severity for weighted scoring
            var tasksCompleted = await _context.Tasks
                .Where(t => t.GroupId == groupId && t.CompletedAt >= startDateTime && t.CompletedAt <= endDateTime)
                .Select(t => new { t.OwnerId, Date = DateOnly.FromDateTime(t.CompletedAt!.Value), t.Priority, t.Severity })
                .ToListAsync();

            // Get messages sent per member per day
            var messagesSent = await _context.GroupMessages
                .Where(m => m.GroupId == groupId && m.CreatedAt >= startDateTime && m.CreatedAt <= endDateTime)
                .Select(m => new { m.UserId, Date = DateOnly.FromDateTime(m.CreatedAt) })
                .ToListAsync();

            // Get comments posted per member per day
            var commentsPosted = await _context.TaskComments
                .Where(c => c.Task.GroupId == groupId && c.CreatedAt >= startDateTime && c.CreatedAt <= endDateTime)
                .Select(c => new { c.UserId, Date = DateOnly.FromDateTime(c.CreatedAt) })
                .ToListAsync();

            // Calculate activity level (0-4) per member per day with weighted scoring
            var allActivity = new Dictionary<(Guid userId, DateOnly date), int>();

            // Tasks: weighted by Priority × Severity (10-40 points per task)
            foreach (var item in tasksCompleted)
            {
                var pWeight = priorityWeight[Math.Min((int)item.Priority, 2)];
                var sWeight = severityWeight[Math.Min((int)item.Severity, 3)];
                var weightedPoints = (int)(CompletePoints * pWeight * sWeight);
                allActivity[(item.OwnerId, item.Date)] = allActivity.GetValueOrDefault((item.OwnerId, item.Date), 0) + weightedPoints;
            }
            // Messages: +1 point
            foreach (var item in messagesSent)
                allActivity[(item.UserId, item.Date)] = allActivity.GetValueOrDefault((item.UserId, item.Date), 0) + 1;
            // Comments: +1 point
            foreach (var item in commentsPosted)
                allActivity[(item.UserId, item.Date)] = allActivity.GetValueOrDefault((item.UserId, item.Date), 0) + 1;

            var maxActivity = allActivity.Values.DefaultIfEmpty(0).Max();

            return memberUserIds.Select(userId =>
            {
                var activityPoints = new List<DailyActivityPoint>();
                for (var date = start; date <= end; date = date.AddDays(1))
                {
                    var rawActivity = allActivity.GetValueOrDefault((userId, date), 0);
                    var level = maxActivity > 0
                        ? rawActivity == 0 ? 0
                            : rawActivity <= maxActivity * 0.25 ? 1
                            : rawActivity <= maxActivity * 0.50 ? 2
                            : rawActivity <= maxActivity * 0.75 ? 3
                            : 4
                        : 0;

                    activityPoints.Add(new DailyActivityPoint
                    {
                        Date = date,
                        ActivityLevel = level
                    });
                }

                return new MemberHeatmapData
                {
                    UserId = userId,
                    UserName = users.GetValueOrDefault(userId, "Unknown"),
                    ActivityByDate = activityPoints
                };
            }).ToList();
        }

        /// <summary>
        /// Get member activity summary with last activity timestamp
        /// Powers Chart 6 (Member Progress Cards)
        /// </summary>
        public async Task<List<MemberActivitySummary>> GetMemberActivitySummaryAsync(
            Guid groupId, DateOnly? startDate = null, DateOnly? endDate = null)
        {
            // Get task breakdown (reuse existing method)
            var taskBreakdown = await GetMemberTaskBreakdownAsync(groupId, startDate, endDate);

            // Get last activity per member
            var lastActivity = await _analyticsRepository.GetMemberLastActivityAsync(groupId);

            // Get contribution percentages
            var totalDone = taskBreakdown.Sum(b => b.DoneTasks);
            var totalActivity = taskBreakdown.Sum(b => b.TotalTasks);

            return taskBreakdown.Select(tb =>
            {
                var contribution = totalActivity > 0
                    ? Math.Round((double)tb.TotalTasks / totalActivity * 100, 2)
                    : 0;

                return new MemberActivitySummary
                {
                    UserId = tb.UserId,
                    UserName = tb.UserName,
                    TotalTasks = tb.TotalTasks,
                    CompletedTasks = tb.DoneTasks,
                    InProgressTasks = tb.InProgressTasks,
                    TodoTasks = tb.TodoTasks,
                    OverdueTasks = tb.OverdueTasks,
                    LastActivityAt = lastActivity.GetValueOrDefault(tb.UserId),
                    ContributionPercentage = contribution,
                    MessagesSent = tb.MessagesSent
                };
            }).ToList();
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
        /// Get studio group comparison
        /// </summary>
        public async Task<List<GroupComparisonData>> GetStudioGroupComparisonAsync(Guid studioId)
        {
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

            // Get all active groups with names in one query
            var groups = await _context.Groups
                .Where(g => g.StudioId == studioId && g.IsActive)
                .Select(g => new { g.GroupId, g.GroupName })
                .ToListAsync();

            if (!groups.Any())
                return new List<GroupComparisonData>();

            var groupIds = groups.Select(g => g.GroupId).ToList();

            // Batch query: total tasks per group
            var totalTasksDict = await _context.Tasks
                .Where(t => t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value))
                .GroupBy(t => t.GroupId!.Value)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.GroupId, x => x.Count);

            // Batch query: completed tasks per group
            var completedTasksDict = await _context.Tasks
                .Where(t => t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value) && t.Progress == 100)
                .GroupBy(t => t.GroupId!.Value)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.GroupId, x => x.Count);

            // Batch query: active members per group (last 30 days)
            var activeMembersDict = await _context.ActivityLogs
                .Where(a => a.GroupId.HasValue && groupIds.Contains(a.GroupId.Value) && a.CreatedAt >= thirtyDaysAgo)
                .GroupBy(a => a.GroupId)
                .Select(g => new { GroupId = g.Key, Count = g.Select(a => a.UserId).Distinct().Count() })
                .ToDictionaryAsync(x => x.GroupId!.Value, x => x.Count);

            // Batch query: last activity datetime per group
            var lastActivityDict = await _context.ActivityLogs
                .Where(a => a.GroupId.HasValue && groupIds.Contains(a.GroupId.Value))
                .GroupBy(a => a.GroupId)
                .Select(g => new { GroupId = g.Key, LastActivity = (DateTime)g.Max(a => a.CreatedAt) })
                .ToDictionaryAsync(x => x.GroupId!.Value, x => x.LastActivity); // DateTime (non-nullable)

            // Batch query: overdue tasks count per group (due date < now && progress < 100)
            var overdueTasksDict = await _context.Tasks
                .Where(t => t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value) && t.DueDate < DateTime.UtcNow && t.Progress < 100)
                .GroupBy(t => t.GroupId!.Value)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.GroupId, x => x.Count);

            var result = groups.Select(g =>
            {
                var totalTasks = totalTasksDict.GetValueOrDefault(g.GroupId, 0);
                var completedTasks = completedTasksDict.GetValueOrDefault(g.GroupId, 0);

                return new GroupComparisonData
                {
                    GroupId = g.GroupId,
                    GroupName = g.GroupName,
                    TotalTasks = totalTasks,
                    CompletedTasks = completedTasks,
                    CompletionRate = totalTasks > 0 ? Math.Round((double)completedTasks / totalTasks * 100, 2) : 0,
                    ActiveMembers = activeMembersDict.GetValueOrDefault(g.GroupId, 0),
                    LastActivityDateTime = lastActivityDict.TryGetValue(g.GroupId, out var lastActivity) ? lastActivity : null,
                    OverdueTasksCount = overdueTasksDict.GetValueOrDefault(g.GroupId, 0)
                };
            }).ToList();

            return result.OrderByDescending(g => g.CompletionRate).ToList();
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

            // Get all active groups in studio
            var groups = await _context.Groups
                .Where(g => g.StudioId == studioId && g.IsActive)
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

        /// <summary>
        /// Get studio group activity heatmap for chart visualization
        /// Returns date x group matrix with activity counts, tasks completed, and intensity levels
        /// </summary>
        public async Task<StudioGroupHeatmapResponse> GetStudioGroupHeatmapAsync(Guid studioId, DateOnly startDate, DateOnly endDate)
        {
            // Get all active groups in studio
            var groups = await _context.Groups
                .Where(g => g.StudioId == studioId && g.IsActive)
                .Select(g => new { g.GroupId, g.GroupName })
                .ToListAsync();

            if (!groups.Any())
            {
                return new StudioGroupHeatmapResponse();
            }

            // Get all activity logs for the studio groups within date range
            var startDateTime = DateTime.SpecifyKind(startDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var endDateTime = DateTime.SpecifyKind(endDate.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

            var groupIds = groups.Select(g => g.GroupId).ToList();

            // Get activity counts per group per day
            var activityLogs = await _context.ActivityLogs
                .Where(a => a.GroupId.HasValue && groupIds.Contains(a.GroupId.Value) && a.CreatedAt >= startDateTime && a.CreatedAt <= endDateTime)
                .GroupBy(a => new { a.GroupId, Date = DateOnly.FromDateTime(a.CreatedAt) })
                .Select(g => new { g.Key.GroupId, g.Key.Date, Count = g.Count() })
                .ToListAsync();

            // Get tasks completed per group per day
            var tasksCompleted = await _context.Tasks
                .Where(t => t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value) && t.CompletedAt.HasValue && t.CompletedAt >= startDateTime && t.CompletedAt <= endDateTime)
                .GroupBy(t => new { t.GroupId, Date = DateOnly.FromDateTime(t.CompletedAt!.Value) })
                .Select(g => new { g.Key.GroupId, g.Key.Date, Count = g.Count() })
                .ToListAsync();

            // Build heatmap data
            var result = new StudioGroupHeatmapResponse();

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var dateActivityLogs = activityLogs.Where(a => a.Date == date).ToList();
                var dateTasksCompleted = tasksCompleted.Where(t => t.Date == date).ToList();

                var groupItems = groups.Select(group =>
                {
                    var activityCount = dateActivityLogs.FirstOrDefault(a => a.GroupId == group.GroupId)?.Count ?? 0;
                    var completedCount = dateTasksCompleted.FirstOrDefault(t => t.GroupId == group.GroupId)?.Count ?? 0;

                    return new StudioGroupActivityItem
                    {
                        GroupId = group.GroupId,
                        GroupName = group.GroupName,
                        ActivityCount = activityCount,
                        TasksCompleted = completedCount
                    };
                }).ToList();

                result.GroupHeatmap.Add(new StudioHeatmapData
                {
                    Date = date,
                    Groups = groupItems
                });
            }

            return result;
        }
    }
}
