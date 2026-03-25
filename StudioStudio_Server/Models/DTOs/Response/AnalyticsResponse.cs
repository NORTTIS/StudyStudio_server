namespace StudioStudio_Server.Models.DTOs.Response
{
    // ==================== USER DASHBOARD ====================

    public class UserDashboardResponse
    {
        public double ProductivityScore { get; set; }
        public List<ActivityHeatmapData> ActivityHeatmap { get; set; } = new();
        public List<TaskCompletionTrendData> TaskCompletionTrend { get; set; } = new();
        public DeadlinePerformanceData DeadlinePerformance { get; set; } = new();
    }

    public class ActivityHeatmapData
    {
        public DateOnly Date { get; set; }
        public int ActivityCount { get; set; }
    }

    public class TaskCompletionTrendData
    {
        public DateOnly Date { get; set; }
        public int TasksCompleted { get; set; }
        public int TasksCreated { get; set; }
    }

    public class DeadlinePerformanceData
    {
        public int OnTimeCount { get; set; }
        public int LateCount { get; set; }
        public double OnTimePercentage { get; set; }
    }

    // ==================== GROUP ANALYTICS ====================

    public class GroupAnalyticsResponse
    {
        public double CompletionRate { get; set; }
        public List<GroupProgressData> Progress { get; set; } = new();
        public List<PerformanceRadarData> PerformanceRadar { get; set; } = new();
        public List<MemberContributionData> MemberContribution { get; set; } = new();
        public List<GroupActivityHeatmapData> ActivityHeatmap { get; set; } = new();

        // === NEW: Full analytics for GroupAnalyticPage ===
        // Chart 1 & 2: Task status breakdown per member
        public List<MemberTaskBreakdownData> MemberTaskBreakdown { get; set; } = new();
        // Chart 3: Per-member daily completion trend
        public List<MemberProgressTrendData> MemberProgressTrend { get; set; } = new();
        // Chart 5: Per-member heatmap activity
        public List<MemberHeatmapData> MemberHeatmap { get; set; } = new();
        // Chart 6: Member activity summary with last activity
        public List<MemberActivitySummary> MemberActivitySummary { get; set; } = new();
    }

    public class GroupProgressData
    {
        public DateOnly Date { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public double CompletionRate { get; set; }
    }

    public class PerformanceRadarData
    {
        public string Metric { get; set; } = string.Empty;
        public double Score { get; set; }
    }

    /// <summary>
    /// Detailed member contribution data with weighted scoring based on priority/severity
    /// Formula: Score = BasePoints × PriorityWeight × SeverityWeight
    /// Priority: Low=1.0, Medium=1.5, High=2.0
    /// Severity: Minor=1.0, Moderate=1.2, Major=1.5, Critical=2.0
    /// Base Points: Complete=10, Create=5, Update=3, Delete=2, Assign=1, Comment=1, Message=1
    /// </summary>
    public class MemberContributionData
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;

        // Task counts
        public int TasksCompleted { get; set; }
        public int TasksCreated { get; set; }
        public int TasksUpdated { get; set; }
        public int TasksDeleted { get; set; }
        public int TasksAssigned { get; set; }
        public int CommentsCreated { get; set; }
        public int MessagesSent { get; set; }

        // Weighted scores
        public double CompletedScore { get; set; }
        public double CreatedScore { get; set; }
        public double UpdatedScore { get; set; }
        public double DeletedScore { get; set; }
        public double AssignedScore { get; set; }

        // Total weighted score for this member
        public double TotalScore { get; set; }
        // Percentage relative to group total
        public double ContributionPercentage { get; set; }
    }

    public class GroupActivityHeatmapData
    {
        public DateOnly Date { get; set; }
        public int ActivityCount { get; set; }
    }

    // ==================== GROUP ANALYTICS ENHANCED (for GroupAnalyticPage) ====================

    /// <summary>
    /// Task status breakdown per member — powers Chart 1 (Personal Donut) & Chart 2 (Group Donut) & Chart 4 (Bar Chart)
    /// </summary>
    public class MemberTaskBreakdownData
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public int TotalTasks { get; set; }
        public int DoneTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int TodoTasks { get; set; }
        public int OverdueTasks { get; set; }
        public double ContributionPercentage { get; set; }
        public int MessagesSent { get; set; }
    }

    /// <summary>
    /// Daily completion point for member progress trend — powers Chart 3 (Line Chart)
    /// </summary>
    public class DailyProgressPoint
    {
        public DateOnly Date { get; set; }
        public int CompletedTasks { get; set; }
    }

    /// <summary>
    /// Per-member daily completion trend — powers Chart 3 (Line Chart)
    /// </summary>
    public class MemberProgressTrendData
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public List<DailyProgressPoint> DailyCompletions { get; set; } = new();
    }

    /// <summary>
    /// Daily activity level (0-4 scale) for heatmap — powers Chart 5 (Member Heatmap)
    /// </summary>
    public class DailyActivityPoint
    {
        public DateOnly Date { get; set; }
        public int ActivityLevel { get; set; } // 0-4 scale
    }

    /// <summary>
    /// Per-member heatmap activity — powers Chart 5 (Member Heatmap)
    /// </summary>
    public class MemberHeatmapData
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public List<DailyActivityPoint> ActivityByDate { get; set; } = new();
    }

    /// <summary>
    /// Member activity summary with last activity timestamp — powers Chart 6 (Member Progress Cards)
    /// </summary>
    public class MemberActivitySummary
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int TodoTasks { get; set; }
        public int OverdueTasks { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public double ContributionPercentage { get; set; }
        public int MessagesSent { get; set; }
    }

    // ==================== STUDIO ANALYTICS ====================

    public class GroupComparisonData
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public double CompletionRate { get; set; }
        public int ActiveMembers { get; set; }
        public DateTime? LastActivityDateTime { get; set; }
        public int OverdueTasksCount { get; set; }
    }

    /// <summary>
    /// Heatmap comparison across groups in a studio
    /// </summary>
    public class GroupHeatmapComparisonData
    {
        public DateOnly Date { get; set; }
        public List<GroupActivityItem> Groups { get; set; } = new();
    }

    public class GroupActivityItem
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public int ActivityCount { get; set; }
        public int MessagesCount { get; set; }
        public int CommentsCount { get; set; }
        public int TasksCompleted { get; set; }
    }

    // ==================== STUDIO GROUP HEATMAP ====================

    public class StudioGroupHeatmapResponse
    {
        public List<StudioHeatmapData> GroupHeatmap { get; set; } = new();
    }

    public class StudioHeatmapData
    {
        public DateOnly Date { get; set; }
        public List<StudioGroupActivityItem> Groups { get; set; } = new();
    }

    public class StudioGroupActivityItem
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public int ActivityCount { get; set; }
        public int TasksCompleted { get; set; }
    }
}
