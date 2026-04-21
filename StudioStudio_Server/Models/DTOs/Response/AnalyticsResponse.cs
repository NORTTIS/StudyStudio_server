namespace StudioStudio_Server.Models.DTOs.Response
{
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
        public double UpdatedScore { get; set; }  // Task updates + Task assign (1 pt flat each)
        public double CommentsScore { get; set; } // Comments only (1 pt flat)
        public double DeletedScore { get; set; }

        // Total weighted score for this member
        public double TotalScore { get; set; }
        // Percentage of this member's total score vs. group total score (score-based)
        public double ContributionScoreRate { get; set; }
    }

    /// <summary>
    /// Per-member contribution result returned by repository (scores + messages).
    /// Used to build MemberContributionData in service for both GroupSummary and GroupRankings.
    /// Scoring: ActivityScoreHelper with assignee credit for TASK_COMPLETE, messages from GroupMessages.
    /// </summary>
    public class MemberContributionResult
    {
        public Guid UserId { get; set; }
        public double TotalScore { get; set; }
        public int MessagesSent { get; set; }
    }


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
        // Intersection counts for Venn diagram
        public int InProgressOverdueTasks { get; set; }  // InProgress ∩ Overdue
        public int TodoOverdueTasks { get; set; }        // Todo ∩ Overdue
        // Percentage of this member's task count vs. group total task count (count-based)
        public double ContributionCountRate { get; set; }
        // Percentage of this member's weighted score vs. group total weighted score (score-based)
        public double ContributionScoreRate { get; set; }
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
        public int ActivityCount { get; set; } // actual weighted score
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
        public double ContributionCountRate { get; set; }
        public int MessagesSent { get; set; }
    }

    /// <summary>
    /// Studio overview response — powers Chart 1 (Group Progress) & Chart 2 (Task Status per group)
    /// Returns studio timeline, status breakdown, and all groups with their task status
    /// </summary>
    public class StudioOverviewResponse
    {
        public Guid StudioId { get; set; }
        public string StartDate { get; set; } = string.Empty;
        public string DueDate { get; set; } = string.Empty;
        public int TotalTasks { get; set; }
        public int TotalGroups { get; set; }
        public StudioStatusBreakdown StatusBreakdown { get; set; } = new();
        public List<StudioGroupData> Groups { get; set; } = new();
    }

    /// <summary>
    /// Task status breakdown for the entire studio
    /// </summary>
    public class StudioStatusBreakdown
    {
        public int Todo { get; set; }
        public int InProgress { get; set; }
        public int Done { get; set; }
        public int Overdue { get; set; }
    }

    /// <summary>
    /// Per-group data for studio overview
    /// </summary>
    public class StudioGroupData
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string GroupColor { get; set; } = string.Empty;
        public int TotalTasks { get; set; }
        public int TotalCompletedTasks { get; set; }
        public int OverdueTasks { get; set; }
        public double CompletionRate { get; set; }
        public int ActiveMembers { get; set; }
        public DateTime? LastActivityDateTime { get; set; }
        /// <summary>
        /// Dynamic task statuses from GroupTaskStatus table
        /// </summary>
        public List<GroupTaskStatusCount> TaskStatuses { get; set; } = new();
    }

    /// <summary>
    /// Dynamic task status count per group
    /// </summary>
    public class GroupTaskStatusCount
    {
        public Guid StatusId { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    // ==================== STUDIO COMPLETION TREND (Chart 3) ====================

    /// <summary>
    /// Studio completion trend response — powers Chart 3 (Line Chart)
    /// Returns cumulative completed tasks per group over time with date filter
    /// </summary>
    public class StudioCompletionTrendResponse
    {
        public List<StudioGroupTrendData> Groups { get; set; } = new();
    }

    /// <summary>
    /// Per-group completion trend data
    /// </summary>
    public class StudioGroupTrendData
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string GroupColor { get; set; } = string.Empty;
        public List<StudioTrendPoint> Points { get; set; } = new();
    }

    /// <summary>
    /// Single data point for completion trend
    /// </summary>
    public class StudioTrendPoint
    {
        public DateOnly Date { get; set; }
        public string Label { get; set; } = string.Empty;
        public int Value { get; set; }
    }


    /// <summary>
    /// Studio group activity response — powers Chart 5 (Activity Heatmap)
    /// Returns activity heatmap with pre-calculated activity level (0-4) per group per day
    ///
    /// Activity Score Formula:
    ///   Score = tasksCompleted × 4 + tasksCreated × 3 + tasksUpdated × 2 + commentsCreated × 1 + messagesSent × 1
    ///
    /// Activity Level Thresholds (FIXED, not dynamic):
    ///   0 = 0 (No activity)
    ///   1 = 1-5 score
    ///   2 = 6-15 score
    ///   3 = 16-30 score
    ///   4 = 31+ score
    /// </summary>
    public class StudioGroupActivityResponse
    {
        public List<StudioActivityRow> Data { get; set; } = new();
    }

    /// <summary>
    /// Single row of heatmap data (one date × all groups)
    /// </summary>
    public class StudioActivityRow
    {
        public string Date { get; set; } = string.Empty;
        public List<StudioActivityItem> Groups { get; set; } = new();
    }

    /// <summary>
    /// Activity data for a single group on a single date
    /// </summary>
    public class StudioActivityItem
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string GroupColor { get; set; } = string.Empty;
        public int ActivityScore { get; set; }
        public int ActivityLevel { get; set; } // 0-4 (pre-calculated)
        public int TasksCompleted { get; set; }
        public int MessagesSent { get; set; }
    }
}
