namespace StudioStudio_Server.Models.DTOs.Request
{
    /// <summary>
    /// Request for hourly activity statistics
    /// </summary>
    public class HourlyActivityRequest
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    /// <summary>
    /// Request for report status statistics
    /// </summary>
    public class ReportStatusRequest
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Period { get; set; } = "monthly"; // "daily" | "weekly" | "monthly"
    }

    /// <summary>
    /// Request for user distribution statistics
    /// </summary>
    public class UserDistributionRequest
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    /// <summary>
    /// Request for subscription distribution statistics
    /// </summary>
    public class SubscriptionDistributionRequest
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    /// <summary>
    /// Request for top active groups
    /// </summary>
    public class TopActiveGroupsRequest
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int TopCount { get; set; } = 5;
    }

    /// <summary>
    /// Request for recent activity
    /// </summary>
    public class RecentActivityRequest
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int ItemCount { get; set; } = 5;
    }
}
