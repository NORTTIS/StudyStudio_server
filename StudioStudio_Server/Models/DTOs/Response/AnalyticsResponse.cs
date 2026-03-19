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

    public class MemberContributionData
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public int TasksCompleted { get; set; }
        public int TasksCreated { get; set; }
        public int MessagesSent { get; set; }
        public double ContributionPercentage { get; set; }
    }

    public class GroupActivityHeatmapData
    {
        public DateOnly Date { get; set; }
        public int ActivityCount { get; set; }
    }

    // ==================== STUDIO ANALYTICS ====================

    public class StudioAnalyticsResponse
    {
        public double CompletionRate { get; set; }
        public int ActiveUsers { get; set; }
        public double EngagementScore { get; set; }
        public List<GroupComparisonData> GroupComparison { get; set; } = new();
        public List<StudioProgressData> CompletionRateHistory { get; set; } = new();
        public List<GroupHeatmapComparisonData> GroupHeatmapComparison { get; set; } = new();
    }

    public class GroupComparisonData
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public double CompletionRate { get; set; }
        public int ActiveMembers { get; set; }
    }

    public class StudioProgressData
    {
        public DateOnly Date { get; set; }
        public double CompletionRate { get; set; }
        public int ActiveUsers { get; set; }
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
}
