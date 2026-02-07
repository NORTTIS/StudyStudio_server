namespace StudioStudio_Server.Models.Entities
{
    public class Report
    {
        public Guid ReportId { get; set; }

        public Guid UserId { get; set; }
        public string Content { get; set; } = null!;

        public ReportStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}