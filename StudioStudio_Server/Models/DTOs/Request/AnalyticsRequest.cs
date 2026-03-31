namespace StudioStudio_Server.Models.DTOs.Request
{
    public class GroupAnalyticsRequest
    {
        public Guid GroupId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
