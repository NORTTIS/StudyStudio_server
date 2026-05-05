using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    /// Service xử lý analytics và dữ liệu dashboard
    public class AnalyticsService(StudioDbContext context, IAnalyticsRepository analyticsRepository, ILogger<AnalyticsService> logger) : IAnalyticsService
    {
        // DTO cho truy vấn phân bổ urgency - tránh anonymous type với List<> inference
        private record UrgencyTaskDto(DateTime? CompletedAt, DateTime? DueDate, int Progress, TaskSeverity Severity);

        // Bảng màu cho nhóm - tạo màu ngẫu nhiên nhất quán
        private static readonly string[] GROUP_COLORS = new[]
        {
            "#3b82f6", "#f97316", "#10b981", "#8b5cf6", "#ec4899",
            "#14b8a6", "#f59e0b", "#6366f1", "#84cc16", "#e11d48"
        };

        /// Trả về màu nhóm, hoặc màu ngẫu nhiên nhất quán nếu ColorHex null/rỗng
        private string GetGroupColor(string? colorHex, Guid groupId)
        {
            // Nếu có ColorHex từ DB thì dùng trực tiếp
            if (!string.IsNullOrWhiteSpace(colorHex))
                return colorHex;

            // Tạo màu ngẫu nhiên nhất quán dựa trên groupId hash
            var hash = groupId.GetHashCode();
            return GROUP_COLORS[Math.Abs(hash) % GROUP_COLORS.Length];
        }

        /// Lấy tóm tắt nhóm (toàn bộ thời gian, không lọc ngày)
        public async Task<GroupSummaryResponse> GetGroupSummaryAsync(Guid groupId, Guid userId)
        {
            // Kiểm tra user có phải thành viên nhóm không
            var isMember = await context.GroupParticipants
                .AnyAsync(p => p.GroupId == groupId && p.UserId == userId);

            // Không phải thành viên -> từ chối truy cập
            if (!isMember)
                throw new AppException(ErrorCodes.GroupPermissionDenied, StatusCodes.Status403Forbidden);

            // Lấy dữ liệu all-time (không lọc ngày)
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

        /// Lấy phân tích công việc theo trạng thái cho toàn bộ nhóm (không theo thành viên)
        /// Chỉ tính các công việc không bị xóa
        private async Task<GroupTaskBreakdownData> GetGroupTaskBreakdownAllTimeAsync(Guid groupId)
        {
            var tasks = await context.Tasks
                .AsNoTracking()
                .Where(t => t.GroupId == groupId && !t.IsPendingDeleted)
                .Select(t => new
                {
                    t.Progress,
                    t.CompletedAt,
                    t.DueDate
                })
                .ToListAsync();

            // Đếm công việc theo trạng thái
            var done = tasks.Count(t => t.Progress == 100 || t.CompletedAt != null);

            // Quá hạn: có DueDate, đã qua hạn, chưa hoàn thành
            var overdue = tasks.Count(t =>
                t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow && t.Progress < 100);

            // Đang làm: tiến độ > 0 và < 100
            var inProgress = tasks.Count(t => t.Progress > 0 && t.Progress < 100);

            // Chưa làm: tiến độ = 0
            var todo = tasks.Count(t => t.Progress == 0);

            // Quá hạn trong các trạng thái 
            var inProgressOverdue = tasks.Count(t =>
                t.Progress > 0 && t.Progress < 100 &&
                t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow);

            var todoOverdue = tasks.Count(t =>
                t.Progress == 0 &&
                t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow);

            // Tổng unique
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

        /// Lấy phân tích công việc theo trạng thái cho từng thành viên (all-time)
        private async Task<List<MemberTaskBreakdownData>> GetMemberTaskBreakdownAllTimeAsync(Guid groupId)
        {
            var breakdown = await analyticsRepository.GetMemberTaskStatusBreakdownAllTimeAsync(groupId);

            // Lấy danh sách userId của các thành viên nhóm
            var memberUserIds = await context.GroupParticipants
                .Where(p => p.GroupId == groupId)
                .Select(p => p.UserId)
                .ToListAsync();

            // Map userId -> FullName
            var users = await context.Users
                .Where(u => memberUserIds.Contains(u.UserId))
                .Select(u => new { u.UserId, FullName = u.FirstName + " " + u.LastName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);

            // Đếm tin nhắn đã gửi cho từng thành viên
            var messagesSent = await context.GroupMessages
                .Where(m => m.GroupId == groupId)
                .GroupBy(m => m.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            // Lấy điểm đóng góp từ repository cho từng thành viên
            var contributionData = await GetGroupMemberContributionAsync(groupId);
            var memberScores = contributionData.ToDictionary(c => c.UserId, c => c.TotalScore);
            var totalScore = memberScores.Values.Sum();

            // Tính tổng hoạt động của tất cả thành viên
            var totalDone = breakdown.Values.Sum(b => b.Done);
            var totalInProgress = breakdown.Values.Sum(b => b.InProgress);
            var totalTodo = breakdown.Values.Sum(b => b.Todo);
            var totalOverdue = breakdown.Values.Sum(b => b.Overdue);
            var totalActivity = totalDone + totalInProgress + totalTodo + totalOverdue;

            // Tính tỷ lệ đóng góp cho từng thành viên
            var result = memberUserIds.Select(userId =>
            {
                var (done, inProgress, todo, overdue, inProgressOverdue, todoOverdue, total) =
                    breakdown.GetValueOrDefault(userId, (0, 0, 0, 0, 0, 0, 0));

                // Tỷ lệ theo số lượng công việc
                var contributionCount = totalActivity > 0
                    ? Math.Round((double)(done + inProgress + todo + overdue) / totalActivity * 100, 2)
                    : 0;

                // Tỷ lệ theo điểm đóng góp
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

            // Sắp xếp theo số công việc hoàn thành giảm dần
            return result.OrderByDescending(r => r.DoneTasks).ToList();
        }

        /// Lấy tóm tắt hoạt động thành viên (all-time)
        private async Task<List<MemberActivitySummary>> GetMemberActivitySummaryAllTimeAsync(Guid groupId)
        {
            var taskBreakdown = await GetMemberTaskBreakdownAllTimeAsync(groupId);
            var lastActivity = await analyticsRepository.GetMemberLastActivityAsync(groupId);

            var totalActivity = taskBreakdown.Sum(b => b.TotalTasks);

            return taskBreakdown.Select(tb =>
            {
                // Tính tỷ lệ đóng góp theo số công việc
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

        /// Lấy dữ liệu đóng góp của từng thành viên (all-time)
        /// Công thức tính điểm: Score = BasePoints × PriorityWeight × SeverityWeight
        /// Priority: Low=1.0, Medium=1.5, High=2.0
        /// Severity: Minor=1.0, Moderate=1.2, Major=1.5, Critical=2.0
        public async Task<List<MemberContributionData>> GetGroupMemberContributionAsync(Guid groupId)
        {
            // Lấy tất cả thành viên nhóm
            var memberUserIds = await context.GroupParticipants
                .Where(p => p.GroupId == groupId)
                .Select(p => p.UserId)
                .ToListAsync();

            // Map userId -> FullName
            var users = await context.Users
                .Where(u => memberUserIds.Contains(u.UserId))
                .Select(u => new { u.UserId, FullName = u.FirstName + " " + u.LastName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);

            // Lấy điểm unified + tin nhắn từ repository
            var repoScores = await analyticsRepository.GetGroupMemberScoresAsync(groupId);

            // Lấy ActivityLogs với priority/severity để tính score breakdown
            var activityLogs = await context.ActivityLogs
                .AsNoTracking()
                .Where(l => l.GroupId == groupId)
                .Select(l => new { l.UserId, l.TargetId, l.ActionType, l.TaskPriority, l.TaskSeverity })
                .ToListAsync();

            // Lấy danh sách task đã hoàn thành để tính assignee credit
            var taskIds = activityLogs
                .Where(l => l.ActionType == "TASK_COMPLETE" && l.TargetId.HasValue)
                .Select(l => l.TargetId!.Value)
                .Distinct()
                .ToList();

            var assignments = await context.TaskAssignments
                .AsNoTracking()
                .Where(a => taskIds.Contains(a.TaskId))
                .Select(a => new { a.TaskId, a.AssignedTo })
                .ToListAsync();

            // Map taskId -> danh sách assignee
            var assigneesByTask = assignments
                .GroupBy(a => a.TaskId)
                .ToDictionary(g => g.Key, g => g.Select(a => a.AssignedTo).ToList());

            // Khởi tạo dữ liệu đóng góp cho từng thành viên
            var memberData = memberUserIds.ToDictionary(
                id => id,
                id => new MemberContributionData { UserId = id }
            );

            // Xử lý ActivityLogs: đếm + tính điểm (assignee credit cho TASK_COMPLETE)
            foreach (var log in activityLogs)
            {
                var priority = log.TaskPriority ?? 0;
                var severity = log.TaskSeverity ?? 0;
                var score = ActivityScoreHelper.GetScore(log.ActionType, priority, severity);

                switch (log.ActionType)
                {
                    case "TASK_COMPLETE":
                        // Nếu task có assignee -> chia điểm cho tất cả assignee
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
                        // Nếu không có assignee -> credit cho người thực hiện
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
                        // Assign là một dạng update action
                        if (memberData.ContainsKey(log.UserId))
                        {
                            memberData[log.UserId].TasksAssigned++;
                            memberData[log.UserId].UpdatedScore += score;
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

            // Cập nhật: MessagesSent từ repo, UserName, và TotalScore cuối cùng
            foreach (var member in memberData.Values)
            {
                member.UserName = users.GetValueOrDefault(member.UserId, "Unknown");
                // Ưu tiên tin nhắn từ GroupMessages table (chính xác hơn)
                if (repoScores.TryGetValue(member.UserId, out var repo))
                    member.MessagesSent = repo.MessagesSent;
                // TotalScore = tổng tất cả thành phần
                member.TotalScore = member.CompletedScore + member.CreatedScore + member.UpdatedScore +
                                    member.CommentsScore + member.DeletedScore + member.MessagesSent;
            }

            // Tính tỷ lệ đóng góp phần trăm
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


        /// Lấy xu hướng tiến độ hoàn thành theo ngày của từng thành viên
        /// Cho Chart 3 (Line Chart)
        public async Task<List<MemberProgressTrendData>> GetMemberProgressTrendAsync(
            Guid groupId,
            DateOnly? startDate = null,
            DateOnly? endDate = null,
            List<Guid>? memberIds = null)
        {
            // Xử lý ngày: dùng UTC Now để tránh shift timezone
            DateOnly end, start;
            if (endDate.HasValue)
            {
                end = endDate.Value;
                start = startDate ?? end.AddDays(-30);
            }
            else
            {
                // Dùng DateTime.UtcNow.Date trực tiếp - tránh shift timezone server
                var utcDate = DateTime.UtcNow.Date;
                end = DateOnly.FromDateTime(utcDate);
                start = end.AddDays(-30);
            }

            // Lấy tất cả thành viên nhóm
            var allMemberIds = await context.GroupParticipants
                .Where(p => p.GroupId == groupId)
                .Select(p => p.UserId)
                .ToListAsync();

            // Nếu có filter memberIds -> lọc theo danh sách đó
            // Nếu không có -> trả về tất cả thành viên
            var targetMemberIds = memberIds?.Any() == true
                ? allMemberIds.Intersect(memberIds).ToList()
                : allMemberIds;

            var users = await context.Users
                .Where(u => targetMemberIds.Contains(u.UserId))
                .Select(u => new { u.UserId, FullName = u.FirstName + " " + u.LastName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);

            // Lấy số công việc hoàn thành theo ngày cho từng thành viên
            var dailyCompletions = await analyticsRepository.GetMemberDailyCompletionsAsync(groupId, start, end);

            return targetMemberIds.Select(userId =>
            {
                var memberDaily = dailyCompletions.GetValueOrDefault(userId, new Dictionary<DateOnly, int>());

                // Tạo danh sách điểm cho mỗi ngày trong khoảng
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

        /// Lấy bản đồ nhiệt hoạt động của từng thành viên (mức 0-4 theo ngày)
        /// Cho Chart 5 (Member Heatmap)
        /// Công thức: Task hoàn thành = 10 × PriorityWeight × SeverityWeight
        public async Task<List<MemberHeatmapData>> GetMemberHeatmapAsync(
            Guid groupId, DateOnly? startDate = null, DateOnly? endDate = null)
        {
            // Mặc định: dùng local time để ngày hôm nay chính xác (UTC+7)
            var end = endDate ?? DateOnly.FromDateTime(DateTime.Now.Date);
            var start = startDate ?? end.AddDays(-30);

            // Chuyển đổi local date range sang UTC cho truy vấn DB
            // Ví dụ: local 2026-04-13 → UTC range [2026-04-12 17:00, 2026-04-13 16:59]
            var zoneId = TimeZoneInfo.TryConvertIanaIdToWindowsId("Asia/Bangkok", out var windowsId)
                ? windowsId
                : "SE Asia Standard Time";
            var tz = TimeZoneInfo.FindSystemTimeZoneById(zoneId);

            // Helper: chuyển local DateOnly -> UTC DateTime
            DateTime ToUtcStart(DateOnly d) => TimeZoneInfo.ConvertTimeToUtc(d.ToDateTime(TimeOnly.MinValue), tz);
            DateTime ToUtcEnd(DateOnly d) => TimeZoneInfo.ConvertTimeToUtc(d.ToDateTime(TimeOnly.MaxValue), tz);

            var startDateTime = DateTime.SpecifyKind(ToUtcStart(start), DateTimeKind.Utc);
            var endDateTime = DateTime.SpecifyKind(ToUtcEnd(end), DateTimeKind.Utc);

            // Helper: chuyển UTC timestamp từ DB -> local DateOnly
            DateOnly ToLocalDate(DateTime utcDt) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utcDt, DateTimeKind.Utc), tz));

            // Hệ số trọng số (giống công thức đóng góp)
            var priorityWeight = new[] { 1.0, 1.5, 2.0 };   // Low, Medium, High
            var severityWeight = new[] { 1.0, 1.2, 1.5, 2.0 };  // Minor, Moderate, Major, Critical
            const double CompletePoints = 10;

            // Lấy danh sách thành viên với tên
            var memberUserIds = await context.GroupParticipants
                .Where(p => p.GroupId == groupId)
                .Select(p => p.UserId)
                .ToListAsync();

            var users = await context.Users
                .Where(u => memberUserIds.Contains(u.UserId))
                .Select(u => new { u.UserId, FullName = u.FirstName + " " + u.LastName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);

            // Lấy tasks đã hoàn thành + assignee hiện tại (tránh abuse từ ActivityLog)
            var completedTaskIds = await context.Tasks
                .Where(t => t.GroupId == groupId && t.CompletedAt >= startDateTime && t.CompletedAt <= endDateTime)
                .Select(t => t.TaskId)
                .ToListAsync();

            var assigneesByTask = new Dictionary<Guid, Guid?>();
            if (completedTaskIds.Count > 0)
            {
                var assignments = await context.TaskAssignments
                    .Where(a => completedTaskIds.Contains(a.TaskId))
                    .Select(a => new { a.TaskId, a.AssignedTo })
                    .ToListAsync();

                assigneesByTask = assignments
                    .GroupBy(a => a.TaskId)
                    .ToDictionary(g => g.Key, g => (Guid?)g.FirstOrDefault()?.AssignedTo);
            }

            var rawCompleted = await context.Tasks
                .Where(t => t.GroupId == groupId && t.CompletedAt >= startDateTime && t.CompletedAt <= endDateTime)
                .Select(t => new { t.TaskId, t.CompletedAt, t.Priority, t.Severity })
                .ToListAsync();

            // Lấy tin nhắn đã gửi theo ngày
            var rawMessages = await context.GroupMessages
                .Where(m => m.GroupId == groupId && m.CreatedAt >= startDateTime && m.CreatedAt <= endDateTime)
                .Select(m => new { m.UserId, m.CreatedAt })
                .ToListAsync();
            var messagesSent = rawMessages
                .Select(m => new { m.UserId, Date = ToLocalDate(m.CreatedAt) })
                .ToList();

            // Lấy comment đã đăng theo ngày
            var rawComments = await context.TaskComments
                .Where(c => c.Task.GroupId == groupId && c.CreatedAt >= startDateTime && c.CreatedAt <= endDateTime)
                .Select(c => new { c.UserId, c.CreatedAt })
                .ToListAsync();
            var commentsPosted = rawComments
                .Select(c => new { c.UserId, Date = ToLocalDate(c.CreatedAt) })
                .ToList();

            // Lấy hoạt động CRUD task từ ActivityLog (CREATE, UPDATE, DELETE)
            var rawCrud = await context.ActivityLogs
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

            // Tính điểm hoạt động cho từng thành viên mỗi ngày
            // Công thức: TASK_COMPLETE → assignee nhận 10×PW×SW (1 lần/task) | CREATE → 3pts | UPDATE → 1pt | DELETE → 1pt | Messages → +1 | Comments → +1
            var allActivity = new Dictionary<(Guid userId, DateOnly date), int>();

            // TASK_COMPLETE: assignee nhận điểm, không assignee → bỏ qua
            foreach (var item in rawCompleted)
            {
                if (!assigneesByTask.TryGetValue(item.TaskId, out var assigneeId) || assigneeId == null)
                    continue;

                var priority = (int)item.Priority;
                var severity = (int)item.Severity;
                var points = (int)(CompletePoints * priorityWeight[Math.Min(priority, 2)] * severityWeight[Math.Min(severity, 3)]);
                var key = (assigneeId.Value, ToLocalDate(item.CompletedAt!.Value));
                allActivity[key] = allActivity.GetValueOrDefault(key, 0) + points;
            }

            // Task CRUD từ ActivityLog: điểm cố định theo loại action
            foreach (var item in taskCrudActivities)
            {
                var priority = item.TaskPriority ?? 0;
                var severity = item.TaskSeverity ?? 0;
                var points = (int)ActivityScoreHelper.GetScore(item.ActionType, priority, severity);
                allActivity[(item.UserId, item.Date)] = allActivity.GetValueOrDefault((item.UserId, item.Date), 0) + points;
            }

            // Tin nhắn: +1 điểm
            foreach (var item in messagesSent)
                allActivity[(item.UserId, item.Date)] = allActivity.GetValueOrDefault((item.UserId, item.Date), 0) + 1;

            // Comments: +1 điểm
            foreach (var item in commentsPosted)
                allActivity[(item.UserId, item.Date)] = allActivity.GetValueOrDefault((item.UserId, item.Date), 0) + 1;

            return memberUserIds.Select(userId =>
            {
                var activityPoints = new List<DailyActivityPoint>();
                for (var date = start; date <= end; date = date.AddDays(1))
                {
                    var rawActivity = allActivity.GetValueOrDefault((userId, date), 0);

                    // Ngưỡng FIXED - tuyệt đối, không tương đối theo nhóm
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

        // ==================== STUDIO OVERVIEW ====================

        /// Lấy tổng quan studio (timeline và tóm tắt các nhóm, không lọc ngày)
        /// Cho Chart 1 (Tiến độ nhóm) & Chart 2 (Trạng thái công việc theo nhóm)
        public async Task<StudioOverviewResponse> GetStudioOverviewAsync(Guid studioId)
        {
            // Lấy thông tin studio
            var studio = await context.Studios
                .Where(s => s.StudioId == studioId)
                .Select(s => new { s.StudioId, s.StartDate, s.EndDate })
                .FirstOrDefaultAsync();

            // Studio không tồn tại -> 404
            if (studio == null)
                throw new AppException(ErrorCodes.GroupNotFound, StatusCodes.Status404NotFound);

            // Lấy tất cả nhóm active với màu sắc
            var groups = await context.Groups
                .Where(g => g.StudioId == studioId && g.IsActive)
                .Select(g => new { g.GroupId, g.GroupName, g.ColorHex })
                .ToListAsync();

            // Không có nhóm -> trả về response rỗng
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

            // Batch query: trạng thái công việc theo nhóm
            var tasks = await context.Tasks
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

            // Batch query: số thành viên active (30 ngày gần nhất)
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var activeMembers = await context.ActivityLogs
                .Where(a => a.GroupId.HasValue && groupIds.Contains(a.GroupId.Value) && a.CreatedAt >= thirtyDaysAgo)
                .GroupBy(a => a.GroupId!.Value)
                .Select(g => new { GroupId = g.Key, Count = g.Select(a => a.UserId).Distinct().Count() })
                .ToDictionaryAsync(x => x.GroupId, x => x.Count);

            // Batch query: hoạt động cuối cùng của từng nhóm
            var lastActivity = await context.ActivityLogs
                .Where(a => a.GroupId.HasValue && groupIds.Contains(a.GroupId.Value))
                .GroupBy(a => a.GroupId)
                .Select(g => new { GroupId = g.Key, Last = g.Max(a => a.CreatedAt) })
                .ToDictionaryAsync(x => x.GroupId!.Value, x => x.Last);

            // Lấy tất cả group statuses cho các nhóm
            var groupStatuses = await context.GroupTaskStatuses
                .Where(s => groupIds.Contains(s.GroupId) && !s.IsDeleted)
                .OrderBy(s => s.Position)
                .ToListAsync();

            // Tính trạng thái cho từng nhóm với dynamic statuses
            var groupDataList = groups.Select(g =>
            {
                var groupTasks = tasks.Where(t => t.GroupId == g.GroupId).ToList();
                var groupStatusList = groupStatuses.Where(s => s.GroupId == g.GroupId).ToList();

                // Đếm công việc quá hạn: chưa hoàn thành, có due date, đã qua hạn
                var overdue = groupTasks.Count(t => t.CompletedAt == null && t.DueDate < DateTime.UtcNow && t.Progress < 100);
                var total = groupTasks.Count;
                var totalCompleted = groupTasks.Count(t => t.CompletedAt != null || t.Progress == 100);

                // Dynamic task statuses từ GroupTaskStatus table
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

            // Tính tổng breakdown cho toàn studio
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

        /// Lấy xu hướng hoàn thành theo nhóm theo thời gian (có bộ lọc ngày)
        /// Cho Chart 3 (Line Chart)
        /// Trả về: Số lượng task hoàn thành tích lũy (cumulative) cho từng nhóm mỗi ngày
        /// Ví dụ: Ngày 3/5 → 10 task, Ngày 4/5 → +1 task → cumulative = 11
        public async Task<StudioCompletionTrendResponse> GetStudioCompletionTrendAsync(
            Guid studioId,
            DateOnly? startDate,
            DateOnly? endDate,
            List<Guid>? groupIds)
        {
            var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var start = startDate ?? end.AddDays(-29);

            // Lấy các nhóm active (hoặc lọc theo groupIds)
            var groupsQuery = context.Groups.Where(g => g.StudioId == studioId && g.IsActive);
            if (groupIds != null && groupIds.Any())
                groupsQuery = groupsQuery.Where(g => groupIds.Contains(g.GroupId));

            var groups = await groupsQuery
                .Select(g => new { g.GroupId, g.GroupName, g.ColorHex })
                .ToListAsync();

            // Không có nhóm -> trả về rỗng
            if (!groups.Any())
                return new StudioCompletionTrendResponse { Groups = new List<StudioGroupTrendData>() };

            var validGroupIds = groups.Select(g => g.GroupId).ToList();
            var startDateTime = DateTime.SpecifyKind(start.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var endDateTime = DateTime.SpecifyKind(end.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

            // Lấy tất cả công việc hoàn thành từ đầu thời gian để tính cumulative đúng
            // (không chỉ từ startDate, để đảm bảo giá trị tích lũy từ quá khứ)
            var completedTasks = await context.Tasks
                .Where(t => t.GroupId.HasValue && validGroupIds.Contains(t.GroupId.Value) &&
                           t.CompletedAt.HasValue && t.CompletedAt <= endDateTime)
                .Select(t => new { t.GroupId, Date = DateOnly.FromDateTime(t.CompletedAt!.Value) })
                .ToListAsync();

            // Tính số lượng task hoàn thành tích lũy cho từng nhóm
            var result = groups.Select(g =>
            {
                var groupCompletions = completedTasks
                    .Where(t => t.GroupId == g.GroupId)
                    .GroupBy(t => t.Date)
                    .ToDictionary(g => g.Key, g => g.Count());

                var points = new List<StudioTrendPoint>();
                // Tính cumulative ban đầu: tổng số task hoàn thành trước ngày `start`
                var preCumulative = groupCompletions.Where(kv => kv.Key < start).Sum(kv => kv.Value);
                var cumulative = preCumulative;

                for (var date = start; date <= end; date = date.AddDays(1))
                {
                    var daily = groupCompletions.GetValueOrDefault(date, 0);
                    cumulative += daily;
                    var dayOfWeek = date.DayOfWeek;
                    // Nhãn ngày: CN, T2, T3...
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

        // ==================== STUDIO GROUP ACTIVITY ====================

        /// Lấy bản đồ nhiệt hoạt động theo nhóm (có bộ lọc ngày, mức 0-4 đã tính sẵn)
        /// Cho Chart 5 (Activity Heatmap)
        ///
        /// Activity Score = tasksCompleted×4 + tasksCreated×3 + tasksUpdated×2 + commentsCreated×1 + messagesSent×1
        /// Activity Level (FIXED thresholds):
        ///   0 = 0 (Không hoạt động)
        ///   1 = 1-5
        ///   2 = 6-15
        ///   3 = 16-30
        ///   4 = 31+
        public async Task<StudioGroupActivityResponse> GetStudioGroupActivityAsync(
            Guid studioId,
            DateOnly? startDate,
            DateOnly? endDate)
        {
            var end = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var start = startDate ?? end.AddDays(-29);
            var startDateTime = DateTime.SpecifyKind(start.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var endDateTime = DateTime.SpecifyKind(end.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

            // Lấy tất cả nhóm active
            var groups = await context.Groups
                .Where(g => g.StudioId == studioId && g.IsActive && g.IsArchived == false)
                .Select(g => new { g.GroupId, g.GroupName, g.ColorHex })
                .ToListAsync();

            // Không có nhóm -> trả về rỗng
            if (!groups.Any())
                return new StudioGroupActivityResponse { Data = new List<StudioActivityRow>() };

            var groupIds = groups.Select(g => g.GroupId).ToList();

            // Lấy công việc hoàn thành theo nhóm theo ngày (có priority/severity)
            var tasksCompleted = await context.Tasks
                .Where(t => t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value) &&
                           t.CompletedAt.HasValue && t.CompletedAt >= startDateTime && t.CompletedAt <= endDateTime)
                .Select(t => new { t.GroupId, Date = DateOnly.FromDateTime(t.CompletedAt!.Value), t.Priority, t.Severity })
                .ToListAsync();

            // Lấy công việc đã tạo theo nhóm theo ngày
            var tasksCreated = await context.Tasks
                .Where(t => t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value) &&
                           t.CreatedAt >= startDateTime && t.CreatedAt <= endDateTime)
                .Select(t => new { t.GroupId, Date = DateOnly.FromDateTime(t.CreatedAt) })
                .ToListAsync();

            // Lấy công việc đã update qua ActivityLogs
            var tasksUpdated = await context.ActivityLogs
                .Where(a => a.GroupId.HasValue && groupIds.Contains(a.GroupId.Value) &&
                           a.ActionType == "TASK_UPDATE" && a.CreatedAt >= startDateTime && a.CreatedAt <= endDateTime)
                .Select(a => new { a.GroupId, Date = DateOnly.FromDateTime(a.CreatedAt) })
                .ToListAsync();

            // Lấy comments theo nhóm theo ngày
            var comments = await context.TaskComments
                .Where(c => c.Task.GroupId.HasValue && groupIds.Contains(c.Task.GroupId.Value) &&
                           c.CreatedAt >= startDateTime && c.CreatedAt <= endDateTime)
                .Select(c => new { GroupId = c.Task.GroupId!.Value, Date = DateOnly.FromDateTime(c.CreatedAt) })
                .ToListAsync();

            // Lấy tin nhắn theo nhóm theo ngày
            var messages = await context.GroupMessages
                .Where(m => groupIds.Contains(m.GroupId) &&
                           m.CreatedAt >= startDateTime && m.CreatedAt <= endDateTime)
                .Select(m => new { m.GroupId, Date = DateOnly.FromDateTime(m.CreatedAt) })
                .ToListAsync();

            // Xây dựng map điểm: (groupId, date) → (tasksCompleted, messagesSent, score)
            var scoreMap = new Dictionary<(Guid groupId, DateOnly date), (int tasksCompleted, int messagesSent, int score)>();

            foreach (var g in groups)
            {
                for (var date = start; date <= end; date = date.AddDays(1))
                {
                    // Chỉ TASK_COMPLETE dùng Priority × Severity
                    // CREATE/UPDATE/DELETE là flat để tránh spam inflate score
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

                    // Các thành phần flat: CREATE=3, UPDATE=1, COMMENT=1, MESSAGE=1
                    var score = (int)completedScore + tcr * 3 + tu * 1 + cm * 1 + ms * 1;

                    scoreMap[(g.GroupId, date)] = (completedOnDay.Count, ms, score);
                }
            }

            // Xây dựng heatmap rows
            var rows = new List<StudioActivityRow>();
            for (var date = start; date <= end; date = date.AddDays(1))
            {
                var groupItems = groups.Select(g =>
                {
                    var value = scoreMap.GetValueOrDefault((g.GroupId, date), (0, 0, 0));
                    var tasksCompletedCount = value.Item1;
                    var messagesSentCount = value.Item2;
                    var score = value.Item3;

                    // Ngưỡng FIXED cho Activity Level
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

        /// Lấy danh sách groupId của user
        private async Task<List<Guid>> GetUserGroupIdsAsync(Guid userId)
        {
            return await context.GroupParticipants
                .AsNoTracking()
                .Where(p => p.UserId == userId && p.Group.IsActive && p.Group.IsArchived == false)
                .Select(p => p.GroupId)
                .ToListAsync();
        }

        /// Lấy tóm tắt KPI của user (tổng công việc, hoàn thành, quá hạn, tỷ lệ, thời gian TB)
        public async Task<UserKpiSummaryResponse> GetUserKpiSummaryAsync(Guid userId)
        {
            var groupIds = await GetUserGroupIdsAsync(userId);
            var now = DateTime.UtcNow;

            // Lấy công việc cá nhân (GroupId = null)
            var personalTasks = await context.Tasks
                .AsNoTracking()
                .Where(t => t.OwnerId == userId && !t.GroupId.HasValue && !t.IsPendingDeleted)
                .Select(t => new { t.Progress, t.CompletedAt, t.DueDate })
                .ToListAsync();

            // Lấy công việc nhóm: chỉ những task được assign cho user
            var groupTasks = await context.Tasks
                .AsNoTracking()
                .Where(t => t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value)
                    && t.IsPendingDeleted == false
                    && context.TaskAssignments.Any(a => a.TaskId == t.TaskId && a.AssignedTo == userId))
                .Select(t => new { t.Progress, t.CompletedAt, t.DueDate })
                .ToListAsync();

            var allTasks = personalTasks.Concat(groupTasks).ToList();

            // Đếm theo trạng thái
            var completed = allTasks.Count(t => t.CompletedAt != null || t.Progress == 100);
            // Đang làm: có tiến độ, chưa quá hạn, chưa hoàn thành
            var inProgress = allTasks.Count(t => t.Progress > 0 && t.Progress < 100 && (!t.DueDate.HasValue || t.DueDate >= now) && t.CompletedAt == null);
            // Quá hạn: chưa hoàn thành, đã quá hạn
            var overdue = allTasks.Count(t => t.CompletedAt == null && t.DueDate < now && t.Progress < 100);
            var total = allTasks.Count;
            var completionRate = total > 0 ? (int)Math.Round((double)completed / total * 100) : 0;

            // Tính thay đổi week-over-week
            var lastWeekTasks = allTasks.Where(t => t.CompletedAt >= now.AddDays(-14) && t.CompletedAt < now.AddDays(-7)).Count();
            var thisWeekTasks = allTasks.Where(t => t.CompletedAt >= now.AddDays(-7)).Count();
            var totalChange = lastWeekTasks > 0 ? (int)Math.Round((double)(thisWeekTasks - lastWeekTasks) / lastWeekTasks * 100) : 0;

            // Thời gian hoàn thành TB (ngày) từ công việc cá nhân
            var completionTimes = await analyticsRepository.GetUserPersonalTaskCompletionTimesAsync(userId);
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

        /// Lấy trạng thái công việc của user (cho donut chart)
        public async Task<UserTaskStatusResponse> GetUserTaskStatusAsync(Guid userId)
        {
            var groupIds = await GetUserGroupIdsAsync(userId);
            var now = DateTime.UtcNow;

            // Lấy công việc cá nhân
            var personalTasks = await context.Tasks
                .AsNoTracking()
                .Where(t => t.OwnerId == userId && !t.GroupId.HasValue && !t.IsPendingDeleted)
                .Select(t => new { t.Progress, t.CompletedAt, t.DueDate })
                .ToListAsync();

            // Lấy công việc nhóm được assign cho user
            var groupTasks = await context.Tasks
                .AsNoTracking()
                .Where(t => t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value)
                    && t.IsPendingDeleted == false
                    && context.TaskAssignments.Any(a => a.TaskId == t.TaskId && a.AssignedTo == userId))
                .Select(t => new { t.Progress, t.CompletedAt, t.DueDate })
                .ToListAsync();

            var all = personalTasks.Concat(groupTasks).ToList();

            // Đếm theo trạng thái
            var completed = all.Count(t => t.CompletedAt != null || t.Progress == 100);
            var overdue = all.Count(t => t.CompletedAt == null && t.DueDate < now && t.Progress < 100);
            var inProgress = all.Count(t => t.Progress > 0 && t.Progress < 100 && (!t.DueDate.HasValue || t.DueDate >= now) && t.CompletedAt == null);
            // Chưa bắt đầu: chưa hoàn thành, tiến độ = 0, chưa quá hạn
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

        /// Lấy xếp hạng nhóm của user qua các studio
        public async Task<UserGroupRankingsResponse> GetUserGroupRankingsAsync(Guid userId)
        {
            var groupIds = await GetUserGroupIdsAsync(userId);

            // Không có nhóm -> trả về rỗng
            if (!groupIds.Any())
                return new UserGroupRankingsResponse { Rankings = new List<GroupRankingItem>() };

            // Lấy tất cả nhóm user tham gia
            var groups = await context.Groups
                .Where(g => groupIds.Contains(g.GroupId)&& g.IsArchived == false && g.IsActive == true)
                .Select(g => new { g.GroupId, g.GroupName })
                .ToListAsync();

            var items = new List<GroupRankingItem>();

            foreach (var g in groups)
            {
                // Lấy điểm per-member + tin nhắn từ repository
                var memberScores = await analyticsRepository.GetGroupMemberScoresAsync(g.GroupId);

                // Không có dữ liệu -> thêm item với score = 0
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

                // Lấy score của user trong nhóm
                var userResult = memberScores.GetValueOrDefault(userId);
                var userScore = userResult?.TotalScore ?? 0;
                var totalGroupScore = memberScores.Values.Sum(m => m.TotalScore);

                // Tính tỷ lệ đóng góp
                var contributionRate = totalGroupScore > 0
                    ? (int)Math.Round(userScore / totalGroupScore * 100)
                    : 0;

                // Tính rank của user trong nhóm
                var userRankWithinGroup = memberScores.Count > 0
                    ? memberScores.Count(m => m.Value.TotalScore > userScore) + 1
                    : 0;

                items.Add(new GroupRankingItem
                {
                    GroupId = g.GroupId,
                    GroupName = g.GroupName,
                    Rank = 0, // sẽ set sau khi sort
                    Score = (int)userScore,
                    ContributionRate = contributionRate,
                    UserRankWithinGroup = userRankWithinGroup
                });
            }

            // Sort theo contributionRate giảm dần, rồi assign rank
            var ranked = items
                .OrderByDescending(x => x.ContributionRate)
                .Select((x, i) => { x.Rank = i + 1; return x; })
                .ToList();

            return new UserGroupRankingsResponse { Rankings = ranked };
        }

        /// Lấy xu hướng năng suất của user (biểu đồ area)
        public async Task<UserProductivityTrendResponse> GetUserProductivityTrendAsync(Guid userId, int periodDays = 30)
        {
            var groupIds = await GetUserGroupIdsAsync(userId);

            // Dùng local time để ngày hôm nay chính xác
            var today = DateOnly.FromDateTime(DateTime.Now);
            var startDate = today.AddDays(-(periodDays - 1));
            var endDate = today;

            // Lấy tất cả công việc của user
            var personalTasks = await context.Tasks
                .AsNoTracking()
                .Where(t => t.OwnerId == userId && !t.GroupId.HasValue && !t.IsPendingDeleted)
                .Select(t => new { t.TaskId, t.CompletedAt, t.CreatedAt, t.DueDate })
                .ToListAsync();

            // Công việc nhóm được assign cho user
            var groupTasks = await context.Tasks
                .AsNoTracking()
                .Where(t => t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value)
                    && t.IsPendingDeleted == false
                    && context.TaskAssignments.Any(a => a.TaskId == t.TaskId && a.AssignedTo == userId))
                .Select(t => new { t.TaskId, t.CompletedAt, t.CreatedAt, t.DueDate })
                .ToListAsync();

            var all = personalTasks.Concat(groupTasks).ToList();
            var trend = new List<ProductivityTrendPoint>();

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                // Npgsql đọc timestamptz là UTC -> chuyển sang local (SE Asia Standard Time / UTC+7) để lấy đúng ngày
                // Ví dụ: DB "2026-03-30 18:23:39 UTC" → Convert về local = "2026-03-31 01:23:39" → DateOnly = 2026-03-31 ✓
                var zoneId = TimeZoneInfo.TryConvertIanaIdToWindowsId("Asia/Ho_Chi_Minh", out var windowsId)
                    ? windowsId
                    : "SE Asia Standard Time";
                DateOnly ToLocalDate(DateTime dt) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(dt, DateTimeKind.Utc),
                    TimeZoneInfo.FindSystemTimeZoneById(zoneId)));

                // Đếm công việc hoàn thành trong ngày (theo local time)
                var completed = all.Count(t => t.CompletedAt.HasValue && ToLocalDate(t.CompletedAt.Value) == date);

                // Tracking quá hạn lifetime: task quá hạn vào ngày D nếu:
                //   1. Chưa hoàn thành VÀ DueDate < D → quá hạn từ DueDate+1
                //   2. Hoàn thành muộn (CompletedAt > DueDate) → quá hạn từ DueDate+1 đến ngày hoàn thành
                // Lưu ý: CompletedAt == DueDate (cùng ngày) = KHÔNG quá hạn (đúng hạn) và kể từ ngày completedAt task đó không còn là overdue nữa
                var overdueTaskIds = all
                    .Where(t =>
                        // Case 1: chưa hoàn thành, đã qua hạn
                        (t.CompletedAt == null && t.DueDate.HasValue && ToLocalDate(t.DueDate.Value) <= date)
                        // Case 2: hoàn thành muộn
                        || (t.CompletedAt.HasValue && t.DueDate.HasValue
                            && ToLocalDate(t.CompletedAt.Value) > ToLocalDate(t.DueDate.Value)
                            && ToLocalDate(t.DueDate.Value) < date)
                            && ToLocalDate(t.CompletedAt.Value) > date)
                    .Select(t => t.TaskId)
                    .ToList();
                logger.LogInformation("overdueTaskIds: {TaskIds}", overdueTaskIds);
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

        /// Lấy phân bổ công việc theo mức ưu tiên
        public async Task<UserPriorityDistributionResponse> GetUserPriorityDistributionAsync(Guid userId)
        {
            var groupIds = await GetUserGroupIdsAsync(userId);
            var byPriority = await analyticsRepository.GetUserTasksByPriorityAsync(groupIds, userId);

            // Nhãn mức ưu tiên
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

        /// Lấy phân bổ công việc theo mức độ khẩn cấp (dựa trên Severity)
        public async Task<UserUrgencyDistributionResponse> GetUserUrgencyDistributionAsync(Guid userId)
        {
            var groupIds = await GetUserGroupIdsAsync(userId);
            var now = DateTime.UtcNow;

            // Urgency = Severity: Critical=Khẩn cấp, Major=Cao, Moderate=Trung bình, Minor=Thấp
            // Dùng explicit record type để tránh anonymous type với List<> inference
            var personalTasks = await context.Tasks
                .AsNoTracking()
                .Where(t => t.OwnerId == userId && !t.GroupId.HasValue && !t.IsPendingDeleted)
                .Select(t => new UrgencyTaskDto(t.CompletedAt, t.DueDate, t.Progress, t.Severity))
                .ToListAsync();

            var groupTasks = await context.Tasks
                .AsNoTracking()
                .Where(t => t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value)
                    && t.IsPendingDeleted == false
                    && context.TaskAssignments.Any(a => a.TaskId == t.TaskId && a.AssignedTo == userId))
                .Select(t => new UrgencyTaskDto(t.CompletedAt, t.DueDate, t.Progress, t.Severity))
                .ToListAsync();

            var all = personalTasks.Concat(groupTasks).ToList();

            // Phân loại task theo severity → urgency bucket
            var khanCap = all.Where(t => t.Severity == TaskSeverity.Critical).ToList();
            var cao = all.Where(t => t.Severity == TaskSeverity.Major).ToList();
            var trungBinh = all.Where(t => t.Severity == TaskSeverity.Moderate).ToList();
            var thap = all.Where(t => t.Severity == TaskSeverity.Minor).ToList();

            // Factory function để tạo UrgencyDistributionItem
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

        /// Lấy benchmark hiệu suất của user so với trung bình nhóm
        public async Task<UserBenchmarkResponse> GetUserBenchmarkAsync(Guid userId, int weeks = 7, Guid? groupId = null)
        {
            var now = DateTime.UtcNow;
            var userGroupIds = await GetUserGroupIdsAsync(userId);

            // Nếu có groupId cụ thể và user thuộc nhóm đó -> dùng nhóm đó
            // Nếu không -> dùng tất cả nhóm của user
            var targetGroupIds = groupId.HasValue && userGroupIds.Contains(groupId.Value)
                ? new List<Guid> { groupId.Value }
                : userGroupIds;

            var weeklyScores = await analyticsRepository.GetUserWeeklyScoresAsync(targetGroupIds, userId, weeks);
            var benchmark = new List<BenchmarkPoint>();

            for (var i = 0; i < weeks; i++)
            {
                // Tính week thứ i (từ quá khứ đến hiện tại)
                var targetDate = now.AddDays(-7 * (weeks - 1 - i));
                var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
                var year = cal.GetYear(targetDate);
                var week = cal.GetWeekOfYear(targetDate, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);

                var found = weeklyScores.FirstOrDefault(x => x.Year == year && x.Week == week);
                var userScore = found.Score;

                // Lấy trung bình nhóm nếu có groupId cụ thể
                var groupAvgRaw = groupId.HasValue
                    ? await analyticsRepository.GetGroupAvgWeeklyScoreAsync(groupId.Value, year, week) ?? (double?)0
                    : 0;

                // Tính trend: trung bình 3 tuần gần nhất
                var recentWeeks = weeklyScores
                    .Where(x => (x.Year < year || (x.Year == year && x.Week <= week)))
                    .OrderByDescending(x => x.Year).ThenByDescending(x => x.Week)
                    .Take(3).ToList();
                var trend = recentWeeks.Count > 0 ? (int)Math.Round(recentWeeks.Average(x => x.Score)) : userScore;

                benchmark.Add(new BenchmarkPoint
                {
                    Week = $"{year}-W{week:D2}",
                    User = userScore,
                    GroupAvg = (int)groupAvgRaw,
                    Trend = trend
                });
            }

            return new UserBenchmarkResponse { Benchmark = benchmark };
        }

        /// Lấy cảnh báo rủi ro (quá hạn, sắp đến hạn, kẹt)
        public async Task<UserRiskAlertsResponse> GetUserRiskAlertsAsync(Guid userId, int limit = 10)
        {
            var groupIds = await GetUserGroupIdsAsync(userId);
            var alerts = new List<RiskAlertItem>();

            // Lấy công việc quá hạn
            var overdueTasks = await analyticsRepository.GetUserOverdueTasksAsync(groupIds, userId, limit);
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

            // Giới hạn số lượng alerts
            return new UserRiskAlertsResponse { Alerts = alerts.Take(limit).ToList() };
        }
    }
}