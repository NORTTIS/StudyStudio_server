namespace StudioStudio_Server.Models.DTOs.Request
{
    public class UserDashboardRequest
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class GroupAnalyticsRequest
    {
        public Guid GroupId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class StudioAnalyticsRequest
    {
        public Guid StudioId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
