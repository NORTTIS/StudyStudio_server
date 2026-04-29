namespace StudioStudio_Server.Models.DTOs.Response
{
    // ==================== PERSONAL ANALYTICS (AnalysisHome) ====================

    /// <summary>
    /// KPI summary for AnalysisHome — personal stats across all groups
    /// </summary>
    public class UserKpiSummaryResponse
    {
        public int TotalTasks { get; set; }
        public int TotalChangePercent { get; set; }       // e.g. 12 = +12% vs last week
        public int Completed { get; set; }
        public int InProgress { get; set; }
        public int CompletionRate { get; set; }           // 0–100
        public int OverdueTasks { get; set; }
        public double AvgCompletionTimeDays { get; set; } // e.g. 2.3
    }

    /// <summary>
    /// Task status distribution for My Task Status donut chart
    /// </summary>
    public class UserTaskStatusResponse
    {
        public List<TaskStatusSegment> Segments { get; set; } = new();
    }

    public class TaskStatusSegment
    {
        public string Name { get; set; } = string.Empty; // "Hoàn thành" | "Đang làm" | "Chưa bắt đầu" | "Quá hạn"
        public int Value { get; set; }
        public string Color { get; set; } = string.Empty; // hex, e.g. "#14b8a6"
    }

    /// <summary>
    /// Cross-studio group rankings for AnalysisHome leaderboard
    /// Aggregates all groups user belongs to across all studios
    /// </summary>
    public class UserGroupRankingsResponse
    {
        public List<GroupRankingItem> Rankings { get; set; } = new();
    }

    public class GroupRankingItem
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public int Rank { get; set; }              // 1 = top — thứ hạng của user so với các nhóm khác
        public int Score { get; set; }             // raw score của user trong nhóm
        public int ContributionRate { get; set; }  // 0–100 % đóng góp so với tổng nhóm
        public int UserRankWithinGroup { get; set; } // thứ hạng của user trong nhóm (vd: #1 contributor)
    }

    /// <summary>
    /// 30-day productivity trend for area chart
    /// </summary>
    public class UserProductivityTrendResponse
    {
        public List<ProductivityTrendPoint> Trend { get; set; } = new();
    }

    public class ProductivityTrendPoint
    {
        public string Date { get; set; } = string.Empty;       // ISO date "2026-03-01"
        public int Completed { get; set; }                     // tasks completed this day
        public int Overdue { get; set; }                       // tasks overdue this day
        public List<Guid> OverdueTaskIds { get; set; } = new(); // task IDs overdue on this day
    }

    /// <summary>
    /// Task distribution by priority level
    /// </summary>
    public class UserPriorityDistributionResponse
    {
        public List<PriorityDistributionItem> Distribution { get; set; } = new();
    }

    public class PriorityDistributionItem
    {
        public string Priority { get; set; } = string.Empty; // "Cao" | "Trung bình" | "Thấp"
        public int Completed { get; set; }
        public int InProgress { get; set; }
        public int Overdue { get; set; }
        public int Todo { get; set; }        // CompletedAt == null && Progress == 0
        public int Total { get; set; }
    }

    /// <summary>
    /// Task distribution by urgency level
    /// </summary>
    public class UserUrgencyDistributionResponse
    {
        public List<UrgencyDistributionItem> Distribution { get; set; } = new();
    }

    public class UrgencyDistributionItem
    {
        public string Urgency { get; set; } = string.Empty; // "Khẩn cấp" | "Cao" | "Trung bình" | "Thấp"
        public int Completed { get; set; }
        public int InProgress { get; set; }
        public int Overdue { get; set; }
        public int Todo { get; set; }        // CompletedAt == null && Progress == 0
        public int Total { get; set; }
        public string AccentColor { get; set; } = string.Empty; // hex for UI accent
    }

    /// <summary>
    /// Weekly performance benchmark (user vs group avg vs trend line)
    /// </summary>
    public class UserBenchmarkResponse
    {
        public List<BenchmarkPoint> Benchmark { get; set; } = new();
    }

    public class BenchmarkPoint
    {
        public string Week { get; set; } = string.Empty; // ISO week "2026-W09"
        public int User { get; set; }                    // personal score 0–100
        public int GroupAvg { get; set; }               // group average score 0–100
        public int Trend { get; set; }                  // smoothed/rolling average 0–100
    }

    /// <summary>
    /// Risk alerts for at-risk tasks (overdue, due soon, stuck)
    /// </summary>
    public class UserRiskAlertsResponse
    {
        public List<RiskAlertItem> Alerts { get; set; } = new();
    }

    public class RiskAlertItem
    {
        public string Type { get; set; } = string.Empty; // "overdue" | "due_soon" | "stuck"
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty; // human-readable
        public string? Group { get; set; }
        public Guid? TaskId { get; set; }
        public string? DueDate { get; set; }             // ISO date for due_soon
    }
}
