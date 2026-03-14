namespace StudioStudio_Server.Models.DTOs.Response
{
    #region Hourly Activity

    /// <summary>
    /// Hourly activity data point
    /// </summary>
    public class HourlyActivityDataPoint
    {
        public int Hour { get; set; } // 0-23
        public int DayOfWeek { get; set; } // 0=Sunday, 1=Monday, etc.
        public string DayName { get; set; } = string.Empty;
        public int UserCount { get; set; }
    }

    /// <summary>
    /// Response for hourly activity statistics
    /// </summary>
    public class HourlyActivityResponse
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<HourlyActivityDataPoint> Data { get; set; } = new();
    }

    #endregion

    #region Report Status

    /// <summary>
    /// Report status data point for each period
    /// </summary>
    public class ReportStatusDataPoint
    {
        public DateTime Date { get; set; }
        public string Period { get; set; } = string.Empty; // T1, T2, etc for months
        public int Pending { get; set; }
        public int Processing { get; set; }
        public int Resolved { get; set; }
        public int Rejected { get; set; }
    }

    /// <summary>
    /// Response for report status statistics
    /// </summary>
    public class ReportStatusResponse
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string PeriodType { get; set; } = string.Empty;
        public List<ReportStatusDataPoint> Data { get; set; } = new();
        public int TotalReports { get; set; }
    }

    #endregion

    #region User Distribution

    /// <summary>
    /// User status distribution item
    /// </summary>
    public class UserDistributionItem
    {
        public string Status { get; set; } = string.Empty; // "Active", "Inactive"
        public int Count { get; set; }
        public decimal Percentage { get; set; }
    }

    /// <summary>
    /// Response for user distribution statistics
    /// </summary>
    public class UserDistributionResponse
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalUsers { get; set; }
        public List<UserDistributionItem> Distribution { get; set; } = new();
    }

    #endregion

    #region Subscription Distribution

    /// <summary>
    /// Subscription plan distribution item
    /// </summary>
    public class SubscriptionDistributionItem
    {
        public string PlanType { get; set; } = string.Empty; // "Free", "Premium"
        public int Count { get; set; }
        public decimal Percentage { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    /// <summary>
    /// Response for subscription distribution statistics
    /// </summary>
    public class SubscriptionDistributionResponse
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalSubscriptions { get; set; }
        public List<SubscriptionDistributionItem> Distribution { get; set; } = new();
    }

    #endregion

    #region Recent Activity

    /// <summary>
    /// Recent activity item
    /// </summary>
    public class RecentActivityItem
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty; // "user_signup", "report_submitted", "premium_upgrade", "group_created", "system_notification"
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int Count { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Response for recent activity
    /// </summary>
    public class RecentActivityResponse
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<RecentActivityItem> Activities { get; set; } = new();
    }

    #endregion

    #region Top Active Groups

    /// <summary>
    /// Top active group item
    /// </summary>
    public class TopActiveGroupItem
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public int MemberCount { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public decimal CompletionRate { get; set; }
        public DateTime LastActivityAt { get; set; }
    }

    /// <summary>
    /// Response for top active groups
    /// </summary>
    public class TopActiveGroupsResponse
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<TopActiveGroupItem> Groups { get; set; } = new();
    }

    #endregion
}
