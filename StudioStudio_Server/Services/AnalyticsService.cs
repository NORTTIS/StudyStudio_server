using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
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

        // DTO for urgency distribution query — avoids anonymous type issues with List<> inference
        private record UrgencyTaskDto(DateTime? CompletedAt, DateTime? DueDate, int Progress, TaskSeverity Severity);

        // Group color palette for generating consistent random colors
        private static readonly string[] GROUP_COLORS = new[]
        {
            "#3b82f6", "#f97316", "#10b981", "#8b5cf6", "#ec4899",
            "#14b8a6", "#f59e0b", "#6366f1", "#84cc16", "#e11d48"
        };

        public AnalyticsService(StudioDbContext context, IAnalyticsRepository analyticsRepository)
        {
            _context = context;
            _analyticsRepository = analyticsRepository;
        }

        /// <summary>
        /// Returns the group's color, or a consistent random color if ColorHex is null/empty.
        /// </summary>
        private string GetGroupColor(string? colorHex, Guid groupId)
        {
            if (!string.IsNullOrWhiteSpace(colorHex))
                return colorHex;

            // Consistent random color based on groupId hash
            var hash = groupId.GetHashCode();
            return GROUP_COLORS[Math.Abs(hash) % GROUP_COLORS.Length];
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
            var groupTaskBreakdown = await GetGroupTaskBreakdownAllTimeAsync(groupId);
            var memberActivitySummary = await GetMemberActivitySummaryAllTimeAsync(groupId);
            var memberContribution = await GetGroupMemberContributionAsync(groupId);

            return new GroupSummaryResponse
            {
                MemberTaskBreakdown = memberTaskBreakdown,
                GroupTaskBreakdown = groupTaskBreakdown,
                MemberActivitySummary = memberActivitySummary,
                MemberContribution = memberContribution
            };
        }

        /// <summary>
        /// Get unique task breakdown for entire group (not per-member) - for Team Chart
        /// Counts each task only once regardless of how many assignees it has
        /// </summary>
        private async Task<GroupTaskBreakdownData> GetGroupTaskBreakdownAllTimeAsync(Guid groupId)
        {
            var tasks = await _context.Tasks
                .AsNoTracking()
                .Where(t => t.GroupId == groupId && !t.IsPendingDeleted)
                .Select(t => new
                {
                    t.Progress,
                    t.CompletedAt,
                    t.DueDate
                })
                .ToListAsync();

            var done = tasks.Count(t => t.Progress == 100 || t.CompletedAt != null);
            var overdue = tasks.Count(t =>
                t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow && t.Progress < 100);
            var inProgress = tasks.Count(t => t.Progress > 0 && t.Progress < 100);
            var todo = tasks.Count(t => t.Progress == 0);
            var inProgressOverdue = tasks.Count(t =>
                t.Progress > 0 && t.Progress < 100 &&
                t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow);
            var todoOverdue = tasks.Count(t =>
                t.Progress == 0 &&
                t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow);

            // Unique total — each task counted once (Venn overlaps excluded)
            var todoOnly = todo - todoOverdue;
            var inProgressOnly = inProgress - inProgressOverdue;
            var totalTasks = todoOnly + inProgressOnly + done + overdue;

            return new GroupTaskBreakdownData
            {
                TotalTasks = totalTasks,
                TodoTasks = todo,
                InProgressTasks = inProgress,
                DoneTasks = done,
                OverdueTasks = overdue,
                InProgressOverdueTasks = inProgressOverdue,
                TodoOverdueTasks = todoOverdue
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

            // Get weighted scores per member for ContributionScoreRate
            var contributionData = await GetGroupMemberContributionAsync(groupId);
            var memberScores = contributionData.ToDictionary(c => c.UserId, c => c.TotalScore);
            var totalScore = memberScores.Values.Sum();

            var totalDone = breakdown.Values.Sum(b => b.Done);
            var totalInProgress = breakdown.Values.Sum(b => b.InProgress);
            var totalTodo = breakdown.Values.Sum(b => b.Todo);
            var totalOverdue = breakdown.Values.Sum(b => b.Overdue);
            var totalActivity = totalDone + totalInProgress + totalTodo + totalOverdue;

            var result = memberUserIds.Select(userId =>
            {
                var (done, inProgress, todo, overdue, inProgressOverdue, todoOverdue, total) =
                    breakdown.GetValueOrDefault(userId, (0, 0, 0, 0, 0, 0, 0));
                var contributionCount = totalActivity > 0
                    ? Math.Round((double)(done + inProgress + todo + overdue) / totalActivity * 100, 2)
                    : 0;
                var contributionScore = totalScore > 0
                    ? Math.Round(memberScores.GetValueOrDefault(userId, 0) / totalScore * 100, 2)
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
                    InProgressOverdueTasks = inProgressOverdue,
                    TodoOverdueTasks = todoOverdue,
                    ContributionCountRate = contributionCount,
                    ContributionScoreRate = contributionScore,
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
                    ContributionCountRate = contribution,
                    MessagesSent = tb.MessagesSent
                };
            }).ToList();
        }

        /// <summary>
        /// Get group member contributions with weighted scoring based on priority/severity
        /// Formula: Score = BasePoints × PriorityWeight × SeverityWeight
        /// Priority: Low=1.0, Medium=1.5, High=2.0
        /// Severity: Minor=1.0, Moderate=1.2, Major=1.5, Critical=2.0
        /// <summary>
        /// Get per-member contribution data for a group — ALL TIME.
        /// Unified scoring via repository (ActivityScoreHelper + assignee credit + GroupMessages).
        /// Used by GroupSummary — same scoring as GroupRankings endpoint.
        /// </summary>
        public async Task<List<MemberContributionData>> GetGroupMemberContributionAsync(Guid groupId)
        {
            // Get all members
            var memberUserIds = await _context.GroupParticipants
                .Where(p => p.GroupId == groupId)
                .Select(p => p.UserId)
                .ToListAsync();

            var users = await _context.Users
                .Where(u => memberUserIds.Contains(u.UserId))
                .Select(u => new { u.UserId, FullName = u.FirstName + " " + u.LastName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);

            // Get unified scores + messages from repository (uses ActivityScoreHelper + assignee credit)
            var repoScores = await _analyticsRepository.GetGroupMemberScoresAsync(groupId);

            // Get ActivityLogs with priority/severity for score breakdown
            var activityLogs = await _context.ActivityLogs
                .AsNoTracking()
                .Where(l => l.GroupId == groupId)
                .Select(l => new { l.UserId, l.TargetId, l.ActionType, l.TaskPriority, l.TaskSeverity })
                .ToListAsync();

            var taskIds = activityLogs
                .Where(l => l.ActionType == "TASK_COMPLETE" && l.TargetId.HasValue)
                .Select(l => l.TargetId!.Value)
                .Distinct()
                .ToList();

            var assignments = await _context.TaskAssignments
                .AsNoTracking()
                .Where(a => taskIds.Contains(a.TaskId))
                .Select(a => new { a.TaskId, a.AssignedTo })
                .ToListAsync();

            var assigneesByTask = assignments
                .GroupBy(a => a.TaskId)
                .ToDictionary(g => g.Key, g => g.Select(a => a.AssignedTo).ToList());

            // Initialize per-user contribution
            var memberData = memberUserIds.ToDictionary(
                id => id,
                id => new MemberContributionData { UserId = id }
            );

            // Process ActivityLogs: counts + scores (assignee credit for TASK_COMPLETE)
            foreach (var log in activityLogs)
            {
                var priority = log.TaskPriority ?? 0;
                var severity = log.TaskSeverity ?? 0;
                var score = ActivityScoreHelper.GetScore(log.ActionType, priority, severity);

                switch (log.ActionType)
                {
                    case "TASK_COMPLETE":
                        if (log.TargetId.HasValue &&
                            assigneesByTask.TryGetValue(log.TargetId.Value, out var assignees) &&
                            assignees.Count > 0)
                        {
                            foreach (var assignee in assignees)
                            {
                                if (!memberData.ContainsKey(assignee)) continue;
                                memberData[assignee].TasksCompleted++;
                                memberData[assignee].CompletedScore += score;
                            }
                        }
                        else if (memberData.ContainsKey(log.UserId))
                        {
                            memberData[log.UserId].TasksCompleted++;
                            memberData[log.UserId].CompletedScore += score;
                        }
                        break;
                    case "TASK_CREATE":
                        if (memberData.ContainsKey(log.UserId))
                        {
                            memberData[log.UserId].TasksCreated++;
                            memberData[log.UserId].CreatedScore += score;
                        }
                        break;
                    case "TASK_UPDATE":
                        if (memberData.ContainsKey(log.UserId))
                        {
                            memberData[log.UserId].TasksUpdated++;
                            memberData[log.UserId].UpdatedScore += score;
                        }
                        break;
                    case "TASK_DELETE":
                        if (memberData.ContainsKey(log.UserId))
                        {
                            memberData[log.UserId].TasksDeleted++;
                            memberData[log.UserId].DeletedScore += score;
                        }
                        break;
                    case "TASK_ASSIGN":
                        if (memberData.ContainsKey(log.UserId))
                        {
                            memberData[log.UserId].TasksAssigned++;
                            memberData[log.UserId].UpdatedScore += score; // Assign is an update action
                        }
                        break;
                    case "COMMENT_CREATE":
                        if (memberData.ContainsKey(log.UserId))
                        {
                            memberData[log.UserId].CommentsCreated++;
                            memberData[log.UserId].CommentsScore += score;
                        }
                        break;
                }
            }

            // Finalize: MessagesSent from repo, then TotalScore
            foreach (var member in memberData.Values)
            {
                member.UserName = users.GetValueOrDefault(member.UserId, "Unknown");
                // Update MessagesSent from repo FIRST (GroupMessages table — more accurate)
                if (repoScores.TryGetValue(member.UserId, out var repo))
                    member.MessagesSent = repo.MessagesSent;
                // TotalScore = all components (UpdatedScore includes TASK_ASSIGN)
                member.TotalScore = member.CompletedScore + member.CreatedScore + member.UpdatedScore +
                                    member.CommentsScore + member.DeletedScore + member.MessagesSent;
            }

            var totalGroupScore = memberData.Values.Sum(m => m.TotalScore);
            if (totalGroupScore > 0)
            {
                foreach (var member in memberData.Values)
                    member.ContributionScoreRate = Math.Round(member.TotalScore / totalGroupScore * 100, 2);
            }

            return memberData.Values
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
                var (done, inProgress, todo, overdue, inProgressOverdue, todoOverdue, total) =
                    breakdown.GetValueOrDefault(userId, (0, 0, 0, 0, 0, 0, 0));
                var contributionCount = totalActivity > 0
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
                    InProgressOverdueTasks = inProgressOverdue,
                    TodoOverdueTasks = todoOverdue,
                    ContributionCountRate = contributionCount,
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
            Guid groupId,
            DateOnly? startDate = null,
            DateOnly? endDate = null,
            List<Guid>? memberIds = null)
        {
            // Resolve end date: use provided value, or default to UTC today
            DateOnly end, start;
            if (endDate.HasValue)
            {
                end = endDate.Value;
                start = startDate ?? end.AddDays(-30);
            }
            else
            {
                // Use UtcNow.Date directly — avoids server timezone shift from DateOnly.FromDateTime(...)
                var utcDate = DateTime.UtcNow.Date;
                end = DateOnly.FromDateTime(utcDate);
                start = end.AddDays(-30);
            }

            // Get all group member IDs
            var allMemberIds = await _context.GroupParticipants
                .Where(p => p.GroupId == groupId)
                .Select(p => p.UserId)
                .ToListAsync();

            // Filter to requested members if provided; otherwise return all
            var targetMemberIds = memberIds?.Any() == true
                ? allMemberIds.Intersect(memberIds).ToList()
                : allMemberIds;

            var users = await _context.Users
                .Where(u => targetMemberIds.Contains(u.UserId))
                .Select(u => new { u.UserId, FullName = u.FirstName + " " + u.LastName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);

            // Get daily completions per member
            var dailyCompletions = await _analyticsRepository.GetMemberDailyCompletionsAsync(groupId, start, end);

            return targetMemberIds.Select(userId =>
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
            // Default to local dates so today's date is correct (user's local timezone = UTC+7)
            var end = endDate ?? DateOnly.FromDateTime(DateTime.Now.Date);
            var start = startDate ?? end.AddDays(-30);

            // User inputs local dates, but DB stores TIMESTAMPTZ (UTC).
            // Convert local date range to UTC for DB query:
            // e.g. local date 2026-04-13 → UTC range [2026-04-12 17:00, 2026-04-13 16:59]
            var zoneId = TimeZoneInfo.TryConvertIanaIdToWindowsId("Asia/Bangkok", out var windowsId)
                ? windowsId
                : "SE Asia Standard Time";
            var tz = TimeZoneInfo.FindSystemTimeZoneById(zoneId);

            DateTime ToUtcStart(DateOnly d) => TimeZoneInfo.ConvertTimeToUtc(d.ToDateTime(TimeOnly.MinValue), tz);
            DateTime ToUtcEnd(DateOnly d) => TimeZoneInfo.ConvertTimeToUtc(d.ToDateTime(TimeOnly.MaxValue), tz);

            var startDateTime = DateTime.SpecifyKind(ToUtcStart(start), DateTimeKind.Utc);
            var endDateTime = DateTime.SpecifyKind(ToUtcEnd(end), DateTimeKind.Utc);

            // Helper: convert UTC timestamp from DB → local DateOnly
            DateOnly ToLocalDate(DateTime utcDt) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utcDt, DateTimeKind.Utc), tz));

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
            var rawTasks = await _context.Tasks
                .Where(t => t.GroupId == groupId && t.CompletedAt >= startDateTime && t.CompletedAt <= endDateTime)
                .Select(t => new { t.OwnerId, t.CompletedAt, t.Priority, t.Severity })
                .ToListAsync();
            var tasksCompleted = rawTasks
                .Select(t => new { t.OwnerId, Date = ToLocalDate(t.CompletedAt!.Value), t.Priority, t.Severity })
                .ToList();

            // Get messages sent per member per day
            var rawMessages = await _context.GroupMessages
                .Where(m => m.GroupId == groupId && m.CreatedAt >= startDateTime && m.CreatedAt <= endDateTime)
                .Select(m => new { m.UserId, m.CreatedAt })
                .ToListAsync();
            var messagesSent = rawMessages
                .Select(m => new { m.UserId, Date = ToLocalDate(m.CreatedAt) })
                .ToList();

            // Get comments posted per member per day
            var rawComments = await _context.TaskComments
                .Where(c => c.Task.GroupId == groupId && c.CreatedAt >= startDateTime && c.CreatedAt <= endDateTime)
                .Select(c => new { c.UserId, c.CreatedAt })
                .ToListAsync();
            var commentsPosted = rawComments
                .Select(c => new { c.UserId, Date = ToLocalDate(c.CreatedAt) })
                .ToList();

            // Get task CRUD activities from ActivityLog (CREATE, UPDATE, DELETE)
            var rawCrud = await _context.ActivityLogs
                .Where(l => l.GroupId == groupId
                    && (l.ActionType == ActivityActionTypes.TASK_CREATE
                        || l.ActionType == ActivityActionTypes.TASK_UPDATE
                        || l.ActionType == ActivityActionTypes.TASK_DELETE)
                    && l.CreatedAt >= startDateTime && l.CreatedAt <= endDateTime)
                .Select(l => new { l.UserId, l.ActionType, l.CreatedAt, l.TaskPriority, l.TaskSeverity })
                .ToListAsync();
            var taskCrudActivities = rawCrud
                .Select(l => new { l.UserId, l.ActionType, Date = ToLocalDate(l.CreatedAt), l.TaskPriority, l.TaskSeverity })
                .ToList();

            // Calculate activity level (0-4) per member per day with weighted scoring
            // Formula: TASK_COMPLETE → 10×PW×SW | TASK_CREATE → 3 pts | TASK_UPDATE → 1 pt | TASK_DELETE → 1 pt | Messages → +1 | Comments → +1
            var allActivity = new Dictionary<(Guid userId, DateOnly date), int>();

            // Tasks COMPLETED: weighted by Priority × Severity (10-40 pts per task)
            foreach (var item in tasksCompleted)
            {
                var pWeight = priorityWeight[Math.Min((int)item.Priority, 2)];
                var sWeight = severityWeight[Math.Min((int)item.Severity, 3)];
                var weightedPoints = (int)(CompletePoints * pWeight * sWeight);
                allActivity[(item.OwnerId, item.Date)] = allActivity.GetValueOrDefault((item.OwnerId, item.Date), 0) + weightedPoints;
            }

            // Task CRUD from ActivityLog: flat points per action type
            foreach (var item in taskCrudActivities)
            {
                var priority = item.TaskPriority ?? 0;
                var severity = item.TaskSeverity ?? 0;
                var points = (int)ActivityScoreHelper.GetScore(item.ActionType, priority, severity);
                allActivity[(item.UserId, item.Date)] = allActivity.GetValueOrDefault((item.UserId, item.Date), 0) + points;
            }

            // Messages: +1 point flat
            foreach (var item in messagesSent)
                allActivity[(item.UserId, item.Date)] = allActivity.GetValueOrDefault((item.UserId, item.Date), 0) + 1;
            // Comments: +1 point flat
            foreach (var item in commentsPosted)
                allActivity[(item.UserId, item.Date)] = allActivity.GetValueOrDefault((item.UserId, item.Date), 0) + 1;

            return memberUserIds.Select(userId =>
            {
                var activityPoints = new List<DailyActivityPoint>();
                for (var date = start; date <= end; date = date.AddDays(1))
                {
                    var rawActivity = allActivity.GetValueOrDefault((userId, date), 0);
                    // FIXED thresholds — absolute, not relative to group max
                    // Level 0: score = 0
                    // Level 1: 0 < score ≤ 5
                    // Level 2: 5 < score ≤ 15
                    // Level 3: 15 < score ≤ 30
                    // Level 4: score > 30
                    var level = rawActivity == 0 ? 0
                        : rawActivity <= 5  ? 1
                        : rawActivity <= 15 ? 2
                        : rawActivity <= 30 ? 3
                        : 4;

                    activityPoints.Add(new DailyActivityPoint
                    {
                        Date = date,
                        ActivityLevel = level,
                        ActivityCount = rawActivity
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
                    ContributionCountRate = contribution,
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

        // ==================== STUDIO OVERVIEW (Chart 1 & 2) ====================

        /// <summary>
        /// Get studio overview with timeline and all groups summary (no date filter)
        /// Powers Chart 1 (Group Progress) & Chart 2 (Task Status per group)
        /// </summary>
        public async Task<StudioOverviewResponse> GetStudioOverviewAsync(Guid studioId)
        {
            // Get studio info
            var studio = await _context.Studios
                .Where(s => s.StudioId == studioId)
                .Select(s => new { s.StudioId, s.StartDate, EndDate = s.EndDate })
                .FirstOrDefaultAsync();

            if (studio == null)
                throw new AppException(Exceptions.ErrorCodes.GroupNotFound, StatusCodes.Status404NotFound);

            // Get all active groups with their colors
            var groups = await _context.Groups
                .Where(g => g.StudioId == studioId && g.IsActive)
                .Select(g => new { g.GroupId, g.GroupName, g.ColorHex })
                .ToListAsync();

            if (!groups.Any())
            {
                return new StudioOverviewResponse
                {
                    StudioId = studioId,
                    StartDate = studio.StartDate?.ToString("yyyy-MM-dd") ?? "",
                    DueDate = studio.EndDate?.ToString("yyyy-MM-dd") ?? "",
                    TotalTasks = 0,
                    TotalGroups = 0,
                    StatusBreakdown = new StudioStatusBreakdown(),
                    Groups = new List<StudioGroupData>()
                };
            }

            var groupIds = groups.Select(g => g.GroupId).ToList();

            // Batch query: task status per group
            var tasks = await _context.Tasks
                .Where(t => t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value))
                .Select(t => new
                {
                    t.GroupId,
                    t.GroupStatusId,
                    t.Progress,
                    t.DueDate,
                    t.CompletedAt
                })
                .ToListAsync();

            // Batch query: active members per group (last 30 days)
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var activeMembers = await _context.ActivityLogs
                .Where(a => a.GroupId.HasValue && groupIds.Contains(a.GroupId.Value) && a.CreatedAt >= thirtyDaysAgo)
                .GroupBy(a => a.GroupId!.Value)
                .Select(g => new { GroupId = g.Key, Count = g.Select(a => a.UserId).Distinct().Count() })
                .ToDictionaryAsync(x => x.GroupId, x => x.Count);

            // Batch query: last activity per group
            var lastActivity = await _context.ActivityLogs
                .Where(a => a.GroupId.HasValue && groupIds.Contains(a.GroupId.Value))
                .GroupBy(a => a.GroupId)
                .Select(g => new { GroupId = g.Key, Last = (DateTime)g.Max(a => a.CreatedAt) })
                .ToDictionaryAsync(x => x.GroupId!.Value, x => x.Last);

            // Get all group statuses for these groups
            var groupStatuses = await _context.GroupTaskStatuses
                .Where(s => groupIds.Contains(s.GroupId) && !s.IsDeleted)
                .OrderBy(s => s.Position)
                .ToListAsync();

            // Calculate per-group status with dynamic statuses
            var groupDataList = groups.Select(g =>
            {
                var groupTasks = tasks.Where(t => t.GroupId == g.GroupId).ToList();
                var groupStatusList = groupStatuses.Where(s => s.GroupId == g.GroupId).ToList();
                var overdue = groupTasks.Count(t => t.CompletedAt == null && t.DueDate < DateTime.UtcNow && t.Progress < 100);
                var total = groupTasks.Count;
                var totalCompleted = groupTasks.Count(t => t.CompletedAt != null || t.Progress == 100);

                // Dynamic task statuses from GroupTaskStatus table
                var taskStatuses = groupStatusList.Select(s => new GroupTaskStatusCount
                {
                    StatusId = s.StatusId,
                    StatusName = s.StatusName,
                    Count = groupTasks.Count(t => t.GroupStatusId == s.StatusId)
                }).ToList();

                return new StudioGroupData
                {
                    GroupId = g.GroupId,
                    GroupName = g.GroupName,
                    GroupColor = GetGroupColor(g.ColorHex, g.GroupId),
                    TotalTasks = total,
                    TotalCompletedTasks = totalCompleted,
                    OverdueTasks = overdue,
                    CompletionRate = total > 0 ? Math.Round((double)totalCompleted / total * 100, 2) : 0,
                    ActiveMembers = activeMembers.GetValueOrDefault(g.GroupId, 0),
                    LastActivityDateTime = lastActivity.TryGetValue(g.GroupId, out var last) ? last : null,
                    TaskStatuses = taskStatuses
                };
            }).ToList();

            // Calculate studio-wide status breakdown (aggregated from dynamic statuses)
            var allTaskStatuses = groupDataList.SelectMany(g => g.TaskStatuses).ToList();
            var statusBreakdown = new StudioStatusBreakdown
            {
                Todo = groupDataList.Sum(g => g.TaskStatuses.Sum(s => s.Count)),
                InProgress = 0,
                Done = groupDataList.Sum(g => g.TaskStatuses.Sum(s => s.Count)),
                Overdue = groupDataList.Sum(g => g.OverdueTasks)
            };

            return new StudioOverviewResponse
            {
                StudioId = studioId,
                StartDate = studio.StartDate?.ToString("yyyy-MM-dd") ?? "",
                DueDate = studio.EndDate?.ToString("yyyy-MM-dd") ?? "",
                TotalTasks = statusBreakdown.Todo + statusBreakdown.InProgress + statusBreakdown.Done + statusBreakdown.Overdue,
                TotalGroups = groups.Count,
                StatusBreakdown = statusBreakdown,
                Groups = groupDataList
            };
        }

        // ==================== STUDIO COMPLETION TREND (Chart 3) ====================

        /// <summary>
        /// Get completion trend per group with date filter
        /// Powers Chart 3 (Line Chart)
        /// Activity Score: tasksCompleted×4 + tasksCreated×3 + tasksUpdated×2 + commentsCreated×1 + messagesSent×1
        /// </summary>
        public async Task<StudioCompletionTrendResponse> GetStudioCompletionTrendAsync(
            Guid studioId,
            DateOnly? startDate,
            DateOnly? endDate,
            List<Guid>? groupIds)
        {
            var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var start = startDate ?? end.AddDays(-29);

            // Get all active groups (or filter by groupIds)
            var groupsQuery = _context.Groups.Where(g => g.StudioId == studioId && g.IsActive);
            if (groupIds != null && groupIds.Any())
                groupsQuery = groupsQuery.Where(g => groupIds.Contains(g.GroupId));

            var groups = await groupsQuery
                .Select(g => new { g.GroupId, g.GroupName, g.ColorHex })
                .ToListAsync();

            if (!groups.Any())
                return new StudioCompletionTrendResponse { Groups = new List<StudioGroupTrendData>() };

            var validGroupIds = groups.Select(g => g.GroupId).ToList();
            var startDateTime = DateTime.SpecifyKind(start.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var endDateTime = DateTime.SpecifyKind(end.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

            // Get completed tasks per group per day
            var completedTasks = await _context.Tasks
                .Where(t => t.GroupId.HasValue && validGroupIds.Contains(t.GroupId.Value) &&
                           t.CompletedAt.HasValue && t.CompletedAt >= startDateTime && t.CompletedAt <= endDateTime)
                .Select(t => new { t.GroupId, Date = DateOnly.FromDateTime(t.CompletedAt!.Value) })
                .ToListAsync();

            var result = groups.Select(g =>
            {
                var groupCompletions = completedTasks
                    .Where(t => t.GroupId == g.GroupId)
                    .GroupBy(t => t.Date)
                    .ToDictionary(g => g.Key, g => g.Count());

                var points = new List<StudioTrendPoint>();
                var cumulative = 0;

                for (var date = start; date <= end; date = date.AddDays(1))
                {
                    var daily = groupCompletions.GetValueOrDefault(date, 0);
                    cumulative += daily;
                    var dayOfWeek = date.DayOfWeek;
                    var label = dayOfWeek == DayOfWeek.Sunday ? "CN"
                        : $"T{(int)dayOfWeek}";

                    points.Add(new StudioTrendPoint
                    {
                        Date = date,
                        Label = label,
                        Value = cumulative
                    });
                }

                return new StudioGroupTrendData
                {
                    GroupId = g.GroupId,
                    GroupName = g.GroupName,
                    GroupColor = GetGroupColor(g.ColorHex, g.GroupId),
                    Points = points
                };
            }).ToList();

            return new StudioCompletionTrendResponse { Groups = result };
        }

        // ==================== STUDIO GROUP STATUS (Chart 4) ====================

        /// <summary>
        /// Get task status breakdown per group with date filter
        /// Powers Chart 4 (Grouped Bar Chart)
        /// </summary>
        public async Task<StudioGroupStatusResponse> GetStudioGroupStatusAsync(
            Guid studioId,
            DateOnly? startDate,
            DateOnly? endDate)
        {
            var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var start = startDate ?? end.AddDays(-29);
            var startDateTime = DateTime.SpecifyKind(start.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var endDateTime = DateTime.SpecifyKind(end.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

            // Get all active groups
            var groups = await _context.Groups
                .Where(g => g.StudioId == studioId && g.IsActive)
                .Select(g => new { g.GroupId, g.GroupName, g.ColorHex })
                .ToListAsync();

            if (!groups.Any())
                return new StudioGroupStatusResponse { Groups = new List<StudioGroupStatusData>() };

            var groupIds = groups.Select(g => g.GroupId).ToList();

            // Get dynamic group statuses
            var groupStatuses = await _context.GroupTaskStatuses
                .Where(s => groupIds.Contains(s.GroupId) && !s.IsDeleted)
                .OrderBy(s => s.Position)
                .ToListAsync();

            // Get tasks within date range with GroupStatusId
            var tasks = await _context.Tasks
                .Where(t => t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value) &&
                           t.CreatedAt >= startDateTime && t.CreatedAt <= endDateTime)
                .Select(t => new
                {
                    t.GroupId,
                    t.GroupStatusId,
                    t.Progress,
                    t.DueDate,
                    t.CompletedAt
                })
                .ToListAsync();

            var result = groups.Select(g =>
            {
                var groupTasks = tasks.Where(t => t.GroupId == g.GroupId).ToList();
                var gStatuses = groupStatuses.Where(s => s.GroupId == g.GroupId).ToList();

                // Dynamic task statuses
                var taskStatuses = gStatuses.Select(s => new GroupTaskStatusCount
                {
                    StatusId = s.StatusId,
                    StatusName = s.StatusName,
                    Count = groupTasks.Count(t => t.GroupStatusId == s.StatusId)
                }).ToList();

                // Legacy counts (fallback if no dynamic statuses)
                return new StudioGroupStatusData
                {
                    GroupId = g.GroupId,
                    GroupName = g.GroupName,
                    GroupColor = GetGroupColor(g.ColorHex, g.GroupId),
                    TaskStatuses = taskStatuses,
                    TodoTasks = groupTasks.Count(t => t.CompletedAt == null && t.Progress == 0),
                    InProgressTasks = groupTasks.Count(t => t.CompletedAt == null && t.Progress > 0 && t.Progress < 100),
                    DoneTasks = groupTasks.Count(t => t.CompletedAt != null || t.Progress == 100),
                    OverdueTasks = groupTasks.Count(t => t.CompletedAt == null && t.DueDate < DateTime.UtcNow && t.Progress < 100)
                };
            }).ToList();

            return new StudioGroupStatusResponse { Groups = result };
        }

        // ==================== STUDIO GROUP ACTIVITY (Chart 5) ====================

        /// <summary>
        /// Get activity heatmap per group with date filter and pre-calculated activity level (0-4)
        /// Powers Chart 5 (Activity Heatmap)
        ///
        /// Activity Score = tasksCompleted×4 + tasksCreated×3 + tasksUpdated×2 + commentsCreated×1 + messagesSent×1
        /// Activity Level (FIXED thresholds):
        ///   0 = 0 (No activity)
        ///   1 = 1-5
        ///   2 = 6-15
        ///   3 = 16-30
        ///   4 = 31+
        /// </summary>
        public async Task<StudioGroupActivityResponse> GetStudioGroupActivityAsync(
            Guid studioId,
            DateOnly? startDate,
            DateOnly? endDate)
        {
            var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var start = startDate ?? end.AddDays(-29);
            var startDateTime = DateTime.SpecifyKind(start.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var endDateTime = DateTime.SpecifyKind(end.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

            // Get all active groups
            var groups = await _context.Groups
                .Where(g => g.StudioId == studioId && g.IsActive)
                .Select(g => new { g.GroupId, g.GroupName, g.ColorHex })
                .ToListAsync();

            if (!groups.Any())
                return new StudioGroupActivityResponse { Data = new List<StudioActivityRow>() };

            var groupIds = groups.Select(g => g.GroupId).ToList();

            // Get tasks completed per group per day WITH priority/severity for weighted scoring
            var tasksCompleted = await _context.Tasks
                .Where(t => t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value) &&
                           t.CompletedAt.HasValue && t.CompletedAt >= startDateTime && t.CompletedAt <= endDateTime)
                .Select(t => new { t.GroupId, Date = DateOnly.FromDateTime(t.CompletedAt!.Value), t.Priority, t.Severity })
                .ToListAsync();

            // Get tasks created per group per day
            var tasksCreated = await _context.Tasks
                .Where(t => t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value) &&
                           t.CreatedAt >= startDateTime && t.CreatedAt <= endDateTime)
                .Select(t => new { t.GroupId, Date = DateOnly.FromDateTime(t.CreatedAt) })
                .ToListAsync();

            // Get tasks updated per group per day (via ActivityLogs)
            var tasksUpdated = await _context.ActivityLogs
                .Where(a => a.GroupId.HasValue && groupIds.Contains(a.GroupId.Value) &&
                           a.ActionType == "TASK_UPDATE" && a.CreatedAt >= startDateTime && a.CreatedAt <= endDateTime)
                .Select(a => new { a.GroupId, Date = DateOnly.FromDateTime(a.CreatedAt) })
                .ToListAsync();

            // Get comments per group per day
            var comments = await _context.TaskComments
                .Where(c => c.Task.GroupId.HasValue && groupIds.Contains(c.Task.GroupId.Value) &&
                           c.CreatedAt >= startDateTime && c.CreatedAt <= endDateTime)
                .Select(c => new { GroupId = c.Task.GroupId!.Value, Date = DateOnly.FromDateTime(c.CreatedAt) })
                .ToListAsync();

            // Get messages per group per day
            var messages = await _context.GroupMessages
                .Where(m => groupIds.Contains(m.GroupId) &&
                           m.CreatedAt >= startDateTime && m.CreatedAt <= endDateTime)
                .Select(m => new { m.GroupId, Date = DateOnly.FromDateTime(m.CreatedAt) })
                .ToListAsync();

            // Build score map: (groupId, date) → score
            var scoreMap = new Dictionary<(Guid groupId, DateOnly date), (int tasksCompleted, int messagesSent, int score)>();

            foreach (var g in groups)
            {
                for (var date = start; date <= end; date = date.AddDays(1))
                {
                    // Weighted scoring: only TASK_COMPLETE uses Priority × Severity
                    // CREATE/UPDATE/DELETE are flat to prevent score inflation via spam
                    var priorityWeight = new[] { 1.0, 1.5, 2.0 };
                    var severityWeight = new[] { 1.0, 1.2, 1.5, 2.0 };
                    var completedOnDay = tasksCompleted.Where(t => t.GroupId == g.GroupId && t.Date == date).ToList();
                    var completedScore = completedOnDay.Sum(t =>
                    {
                        var pw = priorityWeight[Math.Min((int)t.Priority, 2)];
                        var sw = severityWeight[Math.Min((int)t.Severity, 3)];
                        return 10.0 * pw * sw;
                    });
                    var tcr = tasksCreated.Count(t => t.GroupId == g.GroupId && t.Date == date);
                    var tu = tasksUpdated.Count(a => a.GroupId == g.GroupId && a.Date == date);
                    var cm = comments.Count(c => c.GroupId == g.GroupId && c.Date == date);
                    var ms = messages.Count(m => m.GroupId == g.GroupId && m.Date == date);

                    // Flat components: CREATE=3, UPDATE=1, COMMENT=1, MESSAGE=1
                    var score = (int)completedScore + tcr * 3 + tu * 1 + cm * 1 + ms * 1;

                    scoreMap[(g.GroupId, date)] = (completedOnDay.Count, ms, score);
                }
            }

            // Build heatmap rows
            var rows = new List<StudioActivityRow>();
            for (var date = start; date <= end; date = date.AddDays(1))
            {
                var groupItems = groups.Select(g =>
                {
                    var value = scoreMap.GetValueOrDefault((g.GroupId, date), (0, 0, 0));
                    var tasksCompletedCount = value.Item1;
                    var messagesSentCount = value.Item2;
                    var score = value.Item3;

                    var level = score switch
                    {
                        0 => 0,
                        <= 5 => 1,
                        <= 15 => 2,
                        <= 30 => 3,
                        _ => 4
                    };

                    return new StudioActivityItem
                    {
                        GroupId = g.GroupId,
                        GroupName = g.GroupName,
                        GroupColor = GetGroupColor(g.ColorHex, g.GroupId),
                        ActivityScore = score,
                        ActivityLevel = level,
                        TasksCompleted = tasksCompletedCount,
                        MessagesSent = messagesSentCount
                    };
                }).ToList();

                rows.Add(new StudioActivityRow
                {
                    Date = date.ToString("yyyy-MM-dd"),
                    Groups = groupItems
                });
            }

            return new StudioGroupActivityResponse { Data = rows };
        }

        // ==================== PERSONAL ANALYTICS (AnalysisHome) ====================

        private async Task<List<Guid>> GetUserGroupIdsAsync(Guid userId)
        {
            return await _context.GroupParticipants
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .Select(p => p.GroupId)
                .ToListAsync();
        }

        public async Task<UserKpiSummaryResponse> GetUserKpiSummaryAsync(Guid userId)
        {
            var groupIds = await GetUserGroupIdsAsync(userId);
            var now = DateTime.UtcNow;
            var thirtyDaysAgo = now.AddDays(-30);

            // Personal tasks (GroupId = null)
            var personalTasks = await _context.Tasks
                .AsNoTracking()
                .Where(t => t.OwnerId == userId && !t.GroupId.HasValue && !t.IsPendingDeleted)
                .Select(t => new { t.Progress, t.CompletedAt, t.DueDate })
                .ToListAsync();

            // Group tasks: only tasks assigned to this user
            var groupTasks = await _context.Tasks
                .AsNoTracking()
                .Where(t => t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value)
                    && t.IsPendingDeleted == false
                    && _context.TaskAssignments.Any(a => a.TaskId == t.TaskId && a.AssignedTo == userId))
                .Select(t => new { t.Progress, t.CompletedAt, t.DueDate })
                .ToListAsync();

            var allTasks = personalTasks.Concat(groupTasks).ToList();
            var completed = allTasks.Count(t => t.CompletedAt != null || t.Progress == 100);
            var inProgress = allTasks.Count(t => t.Progress > 0 && t.Progress < 100 && (!t.DueDate.HasValue || t.DueDate >= now) && t.CompletedAt == null);
            var overdue = allTasks.Count(t => t.CompletedAt == null && t.DueDate < now && t.Progress < 100);
            var total = allTasks.Count;
            var completionRate = total > 0 ? (int)Math.Round((double)completed / total * 100) : 0;

            // Week-over-week change
            var lastWeekTasks = allTasks.Where(t => t.CompletedAt >= now.AddDays(-14) && t.CompletedAt < now.AddDays(-7)).Count();
            var thisWeekTasks = allTasks.Where(t => t.CompletedAt >= now.AddDays(-7)).Count();
            var totalChange = lastWeekTasks > 0 ? (int)Math.Round((double)(thisWeekTasks - lastWeekTasks) / lastWeekTasks * 100) : 0;

            // Avg completion time (days) from personal tasks
            var completionTimes = await _analyticsRepository.GetUserPersonalTaskCompletionTimesAsync(userId);
            var avgTime = completionTimes.Count > 0 ? Math.Round(completionTimes.Average(), 1) : 0;

            return new UserKpiSummaryResponse
            {
                TotalTasks = total,
                TotalChangePercent = totalChange,
                Completed = completed,
                InProgress = inProgress,
                CompletionRate = completionRate,
                OverdueTasks = overdue,
                AvgCompletionTimeDays = avgTime
            };
        }

        public async Task<UserTaskStatusResponse> GetUserTaskStatusAsync(Guid userId)
        {
            var groupIds = await GetUserGroupIdsAsync(userId);
            var now = DateTime.UtcNow;

            var personalTasks = await _context.Tasks
                .AsNoTracking()
                .Where(t => t.OwnerId == userId && !t.GroupId.HasValue && !t.IsPendingDeleted)
                .Select(t => new { t.Progress, t.CompletedAt, t.DueDate })
                .ToListAsync();

            var groupTasks = await _context.Tasks
                .AsNoTracking()
                .Where(t => t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value)
                    && t.IsPendingDeleted == false
                    && _context.TaskAssignments.Any(a => a.TaskId == t.TaskId && a.AssignedTo == userId))
                .Select(t => new { t.Progress, t.CompletedAt, t.DueDate })
                .ToListAsync();

            var all = personalTasks.Concat(groupTasks).ToList();

            var completed = all.Count(t => t.CompletedAt != null || t.Progress == 100);
            var overdue = all.Count(t => t.CompletedAt == null && t.DueDate < now && t.Progress < 100);
            var inProgress = all.Count(t => t.Progress > 0 && t.Progress < 100 && (!t.DueDate.HasValue || t.DueDate >= now) && t.CompletedAt == null);
            var notStarted = all.Count(t => t.CompletedAt == null && t.Progress == 0 && (!t.DueDate.HasValue || t.DueDate >= now));

            return new UserTaskStatusResponse
            {
                Segments = new List<TaskStatusSegment>
                {
                    new() { Name = "Hoàn thành", Value = completed, Color = "#14b8a6" },
                    new() { Name = "Đang làm", Value = inProgress, Color = "#f97316" },
                    new() { Name = "Chưa bắt đầu", Value = notStarted, Color = "#3b82f6" },
                    new() { Name = "Quá hạn", Value = overdue, Color = "#ef4444" }
                }
            };
        }

        public async Task<UserGroupRankingsResponse> GetUserGroupRankingsAsync(Guid userId)
        {
            var groupIds = await GetUserGroupIdsAsync(userId);
            if (!groupIds.Any())
                return new UserGroupRankingsResponse { Rankings = new List<GroupRankingItem>() };

            // Get all groups the user is a member of
            var groups = await _context.Groups
                .Where(g => groupIds.Contains(g.GroupId))
                .Select(g => new { g.GroupId, g.GroupName })
                .ToListAsync();

            var items = new List<GroupRankingItem>();

            foreach (var g in groups)
            {
                // Get per-member scores + messages (includes GroupMessages via repository)
                var memberScores = await _analyticsRepository.GetGroupMemberScoresAsync(g.GroupId);

                if (!memberScores.Any())
                {
                    items.Add(new GroupRankingItem
                    {
                        GroupId = g.GroupId,
                        GroupName = g.GroupName,
                        Rank = 0,
                        Score = 0,
                        ContributionRate = 0,
                        UserRankWithinGroup = 0
                    });
                    continue;
                }

                var userResult = memberScores.GetValueOrDefault(userId);
                var userScore = userResult?.TotalScore ?? 0;
                var totalGroupScore = memberScores.Values.Sum(m => m.TotalScore);

                var contributionRate = totalGroupScore > 0
                    ? (int)Math.Round(userScore / totalGroupScore * 100)
                    : 0;

                // User's rank within this group
                var userRankWithinGroup = memberScores.Count > 0
                    ? memberScores.Count(m => m.Value.TotalScore > userScore) + 1
                    : 0;

                items.Add(new GroupRankingItem
                {
                    GroupId = g.GroupId,
                    GroupName = g.GroupName,
                    Rank = 0, // set after sorting
                    Score = (int)userScore,
                    ContributionRate = contributionRate,
                    UserRankWithinGroup = userRankWithinGroup
                });
            }

            // Sort by contributionRate descending, then assign rank
            var ranked = items
                .OrderByDescending(x => x.ContributionRate)
                .Select((x, i) => { x.Rank = i + 1; return x; })
                .ToList();

            return new UserGroupRankingsResponse { Rankings = ranked };
        }

        public async Task<UserProductivityTrendResponse> GetUserProductivityTrendAsync(Guid userId, int periodDays = 30)
        {
            var groupIds = await GetUserGroupIdsAsync(userId);
            var now = DateTime.UtcNow;

            // Date range: last `periodDays` days including today (use local time so today's date is correct)
            var today = DateOnly.FromDateTime(DateTime.Now);
            var startDate = today.AddDays(-(periodDays - 1));
            var endDate = today;

            // Get all user's tasks (no CreatedAt filter — date range is for display only)
            var personalTasks = await _context.Tasks
                .AsNoTracking()
                .Where(t => t.OwnerId == userId && !t.GroupId.HasValue && !t.IsPendingDeleted)
                .Select(t => new { t.TaskId, t.CompletedAt, t.CreatedAt, t.DueDate })
                .ToListAsync();

            // Group tasks: only tasks assigned to this user
            var groupTasks = await _context.Tasks
                .AsNoTracking()
                .Where(t => t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value)
                    && t.IsPendingDeleted == false
                    && _context.TaskAssignments.Any(a => a.TaskId == t.TaskId && a.AssignedTo == userId))
                .Select(t => new { t.TaskId, t.CompletedAt, t.CreatedAt, t.DueDate })
                .ToListAsync();

            var all = personalTasks.Concat(groupTasks).ToList();
            var trend = new List<ProductivityTrendPoint>();

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                // Npgsql reads timestamptz as UTC. Convert to local (SE Asia Standard Time / UTC+7) to get the correct calendar date.
                // e.g. DB value: "2026-03-31 01:23:39+07" stored as "2026-03-30 18:23:39 UTC"
                // → ConvertTimeFromUtc(18:23:39 UTC, "SE Asia Standard Time") = 2026-03-31 01:23:39 → DateOnly = 2026-03-31 ✓
                // Cross-platform timezone: prefer IANA "Asia/Bangkok", fall back to Windows ID
                var zoneId = TimeZoneInfo.TryConvertIanaIdToWindowsId("Asia/Bangkok", out var windowsId)
                    ? windowsId
                    : "SE Asia Standard Time";
                DateOnly ToLocalDate(DateTime dt) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(dt, DateTimeKind.Utc),
                    TimeZoneInfo.FindSystemTimeZoneById(zoneId)));

                // Completed: tasks whose CompletedAt (in local time) falls on this date
                var completed = all.Count(t => t.CompletedAt.HasValue && ToLocalDate(t.CompletedAt.Value) == date);

                // Overdue lifetime tracking: a task is overdue on date D if:
                //   1. Never completed AND DueDate < D  → overdue from DueDate+1 onward
                //   2. Completed late (CompletedAt > DueDate) → overdue every day from DueDate+1 through completion date
                // Note: if CompletedAt == DueDate (same day) → NOT overdue (exactly on due date = on time)
                var overdueTaskIds = all
                    .Where(t =>
                        // Case 1: never completed, already past due date
                        (t.CompletedAt == null && t.DueDate.HasValue && ToLocalDate(t.DueDate.Value) <= date)
                        // Case 2: completed late → overdue from DueDate+1 through completion date
                        || (t.CompletedAt.HasValue && t.DueDate.HasValue
                            && ToLocalDate(t.CompletedAt.Value) > ToLocalDate(t.DueDate.Value)
                            && ToLocalDate(t.DueDate.Value) < date))
                    .Select(t => t.TaskId)
                    .ToList();

                trend.Add(new ProductivityTrendPoint
                {
                    Date = date.ToString("yyyy-MM-dd"),
                    Completed = completed,
                    Overdue = overdueTaskIds.Count,
                    OverdueTaskIds = overdueTaskIds
                });
            }

            return new UserProductivityTrendResponse { Trend = trend };
        }

        public async Task<UserOnTimeOverviewResponse> GetUserOnTimeOverviewAsync(Guid userId)
        {
            var groupIds = await GetUserGroupIdsAsync(userId);
            var now = DateTime.UtcNow;

            var personalTasks = await _context.Tasks
                .AsNoTracking()
                .Where(t => t.OwnerId == userId && !t.GroupId.HasValue && !t.IsPendingDeleted && t.DueDate.HasValue)
                .Select(t => new { t.CompletedAt, t.DueDate })
                .ToListAsync();

            var groupTasks = await _context.Tasks
                .AsNoTracking()
                .Where(t => t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value)
                    && t.IsPendingDeleted == false
                    && _context.TaskAssignments.Any(a => a.TaskId == t.TaskId && a.AssignedTo == userId)
                    && t.DueDate.HasValue)
                .Select(t => new { t.CompletedAt, t.DueDate })
                .ToListAsync();

            var all = personalTasks.Concat(groupTasks).ToList();

            var onTime = all.Count(t => t.CompletedAt != null && t.DueDate.HasValue && t.CompletedAt <= t.DueDate.Value);
            var overdue = all.Count(t => t.CompletedAt == null && t.DueDate < now);

            return new UserOnTimeOverviewResponse
            {
                Segments = new List<TaskStatusSegment>
                {
                    new() { Name = "Đúng hạn", Value = onTime, Color = "#14b8a6" },
                    new() { Name = "Quá hạn", Value = overdue, Color = "#ef4444" }
                }
            };
        }

        public async Task<UserPriorityDistributionResponse> GetUserPriorityDistributionAsync(Guid userId)
        {
            var groupIds = await GetUserGroupIdsAsync(userId);
            var byPriority = await _analyticsRepository.GetUserTasksByPriorityAsync(groupIds, userId);

            var priorityLabels = new[] { "Thấp", "Trung bình", "Cao" };

            var items = byPriority.Select(x => new PriorityDistributionItem
            {
                Priority = priorityLabels[Math.Min(x.Priority, 2)],
                Completed = x.Done,
                InProgress = x.InProgress,
                Overdue = x.Overdue,
                Todo = x.Todo,
                Total = x.Total
            }).ToList();

            return new UserPriorityDistributionResponse { Distribution = items };
        }

        public async Task<UserUrgencyDistributionResponse> GetUserUrgencyDistributionAsync(Guid userId)
        {
            var groupIds = await GetUserGroupIdsAsync(userId);
            var now = DateTime.UtcNow;

            // Urgency = based on Severity of task (Critical=Khẩn cấp, Major=Cao, Moderate=Trung bình, Minor=Thấp)
            // Use explicit record type to avoid anonymous type issues with List<> inference
            var personalTasks = await _context.Tasks
                .AsNoTracking()
                .Where(t => t.OwnerId == userId && !t.GroupId.HasValue && !t.IsPendingDeleted)
                .Select(t => new UrgencyTaskDto(t.CompletedAt, t.DueDate, t.Progress, t.Severity))
                .ToListAsync();

            var groupTasks = await _context.Tasks
                .AsNoTracking()
                .Where(t => t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value)
                    && t.IsPendingDeleted == false
                    && _context.TaskAssignments.Any(a => a.TaskId == t.TaskId && a.AssignedTo == userId))
                .Select(t => new UrgencyTaskDto(t.CompletedAt, t.DueDate, t.Progress, t.Severity))
                .ToListAsync();

            var all = personalTasks.Concat(groupTasks).ToList();

            // Classify each task by severity → urgency bucket
            var khanCap = all.Where(t => t.Severity == TaskSeverity.Critical).ToList();
            var cao = all.Where(t => t.Severity == TaskSeverity.Major).ToList();
            var trungBinh = all.Where(t => t.Severity == TaskSeverity.Moderate).ToList();
            var thap = all.Where(t => t.Severity == TaskSeverity.Minor).ToList();

            UrgencyDistributionItem MakeItem(string label, List<UrgencyTaskDto> bucket, string accentColor)
            {
                var done = bucket.Count(t => t.CompletedAt != null || t.Progress == 100);
                var inProgress = bucket.Count(t => t.CompletedAt == null && t.Progress > 0 && t.Progress < 100 && (!t.DueDate.HasValue || t.DueDate >= now));
                var overdue = bucket.Count(t => t.CompletedAt == null && t.DueDate < now && t.Progress < 100);
                var todo = bucket.Count(t => t.CompletedAt == null && t.Progress == 0 && t.DueDate >= now);
                return new UrgencyDistributionItem
                {
                    Urgency = label,
                    Total = bucket.Count,
                    Completed = done,
                    InProgress = inProgress,
                    Overdue = overdue,
                    Todo = todo,
                    AccentColor = accentColor
                };
            }

            var urgencyItems = new List<UrgencyDistributionItem>
            {
                MakeItem("Khẩn cấp", khanCap, "#dc2626"),
                MakeItem("Cao", cao, "#ea580c"),
                MakeItem("Trung bình", trungBinh, "#ca8a04"),
                MakeItem("Thấp", thap, "#0d9488")
            };

            return new UserUrgencyDistributionResponse { Distribution = urgencyItems };
        }

        public async Task<UserBenchmarkResponse> GetUserBenchmarkAsync(Guid userId, int weeks = 7, Guid? groupId = null)
        {
            var now = DateTime.UtcNow;
            var userGroupIds = await GetUserGroupIdsAsync(userId);
            var targetGroupIds = groupId.HasValue && userGroupIds.Contains(groupId.Value)
                ? new List<Guid> { groupId.Value }
                : userGroupIds;

            var weeklyScores = await _analyticsRepository.GetUserWeeklyScoresAsync(targetGroupIds, userId, weeks);
            var benchmark = new List<BenchmarkPoint>();

            for (var i = 0; i < weeks; i++)
            {
                var targetDate = now.AddDays(-7 * (weeks - 1 - i));
                var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
                var year = cal.GetYear(targetDate);
                var week = cal.GetWeekOfYear(targetDate, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);

                var found = weeklyScores.FirstOrDefault(x => x.Year == year && x.Week == week);
                var userScore = found.Score;
                var groupAvgRaw = groupId.HasValue
                    ? await _analyticsRepository.GetGroupAvgWeeklyScoreAsync(groupId.Value, year, week) ?? (double?)0
                    : (double?)0;

                // Rolling 3-week trend average
                var recentWeeks = weeklyScores
                    .Where(x => (x.Year < year || (x.Year == year && x.Week <= week)))
                    .OrderByDescending(x => x.Year).ThenByDescending(x => x.Week)
                    .Take(3).ToList();
                var trend = recentWeeks.Count > 0 ? (int)Math.Round(recentWeeks.Average(x => x.Score)) : userScore;

                benchmark.Add(new BenchmarkPoint
                {
                    Week = $"{year}-W{week:D2}",
                    User = userScore,
                    GroupAvg = (int)(groupAvgRaw ?? 0),
                    Trend = trend
                });
            }

            return new UserBenchmarkResponse { Benchmark = benchmark };
        }

        public async Task<UserRiskAlertsResponse> GetUserRiskAlertsAsync(Guid userId, int limit = 10)
        {
            var groupIds = await GetUserGroupIdsAsync(userId);
            var alerts = new List<RiskAlertItem>();

            var overdueTasks = await _analyticsRepository.GetUserOverdueTasksAsync(groupIds, userId, limit);
            foreach (var t in overdueTasks)
            {
                var daysOverdue = (DateTime.UtcNow - t.DueDate).Days;
                alerts.Add(new RiskAlertItem
                {
                    Type = "overdue",
                    Title = t.Title,
                    Description = $"Đã quá hạn {daysOverdue} ngày",
                    Group = t.GroupName,
                    TaskId = t.TaskId,
                    DueDate = t.DueDate.ToString("yyyy-MM-dd")
                });
            }

            var dueSoonTasks = await _analyticsRepository.GetUserDueSoonTasksAsync(groupIds, userId, 1, limit);
            foreach (var t in dueSoonTasks)
            {
                var daysUntil = (t.DueDate - DateTime.UtcNow).Days;
                alerts.Add(new RiskAlertItem
                {
                    Type = "due_soon",
                    Title = t.Title,
                    Description = daysUntil <= 0 ? "Hạn chót: Hôm nay" : $"Hạn chót: Ngày mai",
                    Group = t.GroupName,
                    TaskId = t.TaskId,
                    DueDate = t.DueDate.ToString("yyyy-MM-dd")
                });
            }

            var stuckTasks = await _analyticsRepository.GetUserStuckTasksAsync(groupIds, userId, 5, limit);
            foreach (var t in stuckTasks)
            {
                var daysNoUpdate = (int)(DateTime.UtcNow - t.LastUpdated).TotalDays;
                alerts.Add(new RiskAlertItem
                {
                    Type = "stuck",
                    Title = t.Title,
                    Description = $"Không cập nhật {daysNoUpdate} ngày",
                    Group = t.GroupName,
                    TaskId = t.TaskId
                });
            }

            return new UserRiskAlertsResponse { Alerts = alerts.Take(limit).ToList() };
        }
    }
}
